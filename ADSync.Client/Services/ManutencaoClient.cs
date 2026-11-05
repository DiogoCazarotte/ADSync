using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ADSync.Application.DTOs;
using Microsoft.JSInterop;

namespace ADSync.Client.Services;

public sealed partial class ManutencaoClient(HttpClient http, IJSRuntime js, Microsoft.Extensions.Configuration.IConfiguration configuration) : IAsyncDisposable
{
    public bool ReadOnlyMode { get; } = bool.TryParse(configuration["ReadOnlyMode"], out var value) && value;
    private string StoragePrefix => string.IsNullOrWhiteSpace(configuration["StorageScope"])
        ? "nexservice" : $"nexservice:{configuration["StorageScope"]}";
    private string SessionKey => ReadOnlyMode ? $"{StoragePrefix}:readonly:session" : $"{StoragePrefix}:session";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private Task? loop;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public LoginResponseDto? Session { get; private set; }
    public bool Busy { get; private set; }
    public string Notice { get; private set; } = "";
    public int Pending { get; private set; }
    public event Action? Changed;
    private string Key(string suffix) => $"{StoragePrefix}:{Session!.Email.ToLowerInvariant()}:{suffix}";
    public bool Role(string role) => Session?.Papeis.Contains(role) == true;

    private async Task<T?> Read<T>(string area, string key)
    {
        var raw = await js.InvokeAsync<string?>("nexStorage.get", area, key);
        return raw is null ? default : JsonSerializer.Deserialize<T>(raw, Json);
    }
    private ValueTask Write<T>(string area, string key, T value) =>
        js.InvokeVoidAsync("nexStorage.set", area, key, JsonSerializer.Serialize(value, Json));

    private async Task<T> Locked<T>(Func<Task<T>> action)
    {
        if (!await gate.WaitAsync(TimeSpan.FromSeconds(20), stop.Token))
            throw new InvalidOperationException("Outra operação ainda está em andamento. Aguarde e tente novamente.");
        Busy = true;
        var lockId = Guid.NewGuid().ToString();
        var acquired = false;
        try
        {
            if (TechnicalOfflineEnabled) { await js.InvokeVoidAsync("nexStorage.acquire", StoragePrefix, lockId); acquired = true; }
            Changed?.Invoke(); return await action();
        }
        finally
        {
            try { if (acquired) await js.InvokeVoidAsync("nexStorage.release", lockId); }
            finally { Busy = false; gate.Release(); Changed?.Invoke(); }
        }
    }
    public async Task InitializeAsync()
    {
        await Locked(async () =>
        {
            Session = await Read<LoginResponseDto>("session", SessionKey);
            if (Session is not null) await UpdatePending();
            return true;
        });
        loop ??= RunTimer();
    }
    public Task LoginAsync(LoginDto login) => Locked(async () =>
    {
        var session = await Send<LoginResponseDto>(HttpMethod.Post, "api/auth/login", login, false);
        await Write("session", SessionKey, session);
        Session = session;
        await UpdatePending();
        Notice = "";
        return true;
    });
    public Task LogoutAsync() => Locked(async () =>
    {
        await js.InvokeVoidAsync("nexStorage.remove", "session", SessionKey);
        Session = null; Pending = 0; Notice = ""; technicalDetails.Clear(); pendingOrders.Clear(); PreparedAt = null; lastPreparationAttempt = default;
        PreparationNotice = "Preparação automática aguardando conexão.";
        return true;
    });
    private Task<List<CriarOrdemServicoDto>?> ReadQueue() => Read<List<CriarOrdemServicoDto>>("local", Key("queue"));
    private async Task<List<CriarOrdemServicoDto>> Queue() => await ReadQueue() ?? [];

    public Task OpenAsync(CriarOrdemServicoDto draft) => Locked(async () =>
    {
        if (ReadOnlyMode) throw new InvalidOperationException("Validação somente leitura: abertura desativada.");
        if (!(Role("Operador") || Role("Gestor"))) throw new InvalidOperationException("Entre como operador ou gestor.");
        draft.ClientSyncId ??= Guid.NewGuid();
        draft.DataAbertura ??= DateTimeOffset.UtcNow;
        draft.CriadoOffline = true;
        var queue = await Queue();
        if (!queue.Any(x => x.ClientSyncId == draft.ClientSyncId)) queue.Add(draft);
        // Persist first. Storage errors propagate so the UI retains the form.
        await Write("local", Key("queue"), queue);
        await UpdatePending();
        await SyncCore();
        return true;
    });
    public async Task SyncAsync()
    {
        await Locked(async () => { await SyncCore(); return true; });
        await SyncTechnicalSafe();
    }
    private async Task SyncCore()
    {
        if (ReadOnlyMode) { Notice = "Somente leitura: pendências preservadas; envio desativado."; return; }
        if (Session is null) return;
        var queue = await Queue();
        await UpdatePending();
        if (queue.Count == 0) return;
        while (queue.Count > 0)
        {
            try
            {
                await Send<OrdemServicoDto>(HttpMethod.Post, "api/ordensservico", queue[0]);
                var remaining = queue.Skip(1).ToList();
                await Write("local", Key("queue"), remaining);
                queue = remaining;
                await UpdatePending();
            }
            catch (Exception ex)
            {
                Notice = $"{Pending} pendência(s) preservada(s) no dispositivo. {ex.Message}";
                return;
            }
        }
        Notice = "Chamados sincronizados.";
    }
    public Task<T> GetOnlineAsync<T>(string path) => Locked(() => Send<T>(HttpMethod.Get, path));
    public Task<T> GetAsync<T>(string path) => Locked(async () =>
    {
        if (Session is null) throw new InvalidOperationException("Entre novamente.");
        // Old demonstration caches must not be presented as Supabase data.
        if (ReadOnlyMode) return await Send<T>(HttpMethod.Get, path);
        var key = Key("cache:" + path);
        T result;
        try { result = await Send<T>(HttpMethod.Get, path); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var cached = await Read<CacheEntry<T>>("local", key);
            if (cached is null) throw;
            Notice = $"Cache desatualizado de {cached.At.LocalDateTime:g}. {ex.Message}";
            return cached.Value;
        }
        try { await Write("local", key, new CacheEntry<T>(DateTimeOffset.UtcNow, result)); }
        catch (JSException) { Notice = "Consulta recebida, mas não foi possível salvar o cache local."; }
        return result;
    });
    // Technical actions are online only, and are never replayed automatically.
    public Task<T> PostAsync<T>(string path, object body) => Locked(() => ReadOnlyMode
        ? throw new InvalidOperationException("Validação somente leitura: ação desativada.")
        : Send<T>(HttpMethod.Post, path, body));
    public Task<T> PutAsync<T>(string path, object body) => Locked(() => ReadOnlyMode
        ? throw new InvalidOperationException("Validação somente leitura: ação desativada.")
        : Send<T>(HttpMethod.Put, path, body));
    private async Task<T> Send<T>(HttpMethod method, string path, object? body = null, bool authenticated = true)
    {
        if (TechnicalOfflineEnabled && !await js.InvokeAsync<bool>("nexStorage.isOnline"))
            throw new HttpRequestException("Sem conexão. Os registros permanecem salvos no dispositivo para sincronizar depois.");
        using var request = new HttpRequestMessage(method, path);
        if (authenticated)
        {
            if (Session is null) throw new InvalidOperationException("Entre novamente.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.Token);
        }
        if (body is not null) request.Content = JsonContent.Create(body);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 408 or 429 or >= 500)
                    throw new HttpRequestException("Serviço temporariamente indisponível. Os registros locais foram preservados; a sincronização tentará novamente.", null, response.StatusCode);
                var detail = await response.Content.ReadAsStringAsync(timeout.Token);
                throw new InvalidOperationException($"API {(int)response.StatusCode}: {detail}");
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: timeout.Token) ?? throw new InvalidOperationException("Resposta vazia da API.");
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            throw new TaskCanceledException(method == HttpMethod.Get
                ? "A consulta demorou demais. Verifique a conexão e tente atualizar novamente."
                : body is EventoAtendimentoDto
                    ? "O prazo de confirmação da sincronização terminou. O evento permanece na fila e será reenviado automaticamente; não repita o atendimento."
                : "O servidor não confirmou a operação a tempo. Atualize a consulta para conferir se ela foi concluída antes de salvar novamente.");
        }
    }
    private async Task RunTimer()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                try { if (!ReadOnlyMode && Session is not null && !Busy) { await SyncAsync(); PrepareInBackground(); } }
                catch (Exception ex) { Notice = ex.Message; Changed?.Invoke(); }
            }
        }
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        if (loop is not null) await loop;
        if (preparationTask is not null) await preparationTask;
        stop.Dispose();
    }
    private sealed record CacheEntry<T>(DateTimeOffset At, T Value);
}
