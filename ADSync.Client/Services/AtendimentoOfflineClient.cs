using System.Net.Http;
using System.Text.Json;
using ADSync.Application.DTOs;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using Microsoft.JSInterop;

namespace ADSync.Client.Services;

public sealed partial class ManutencaoClient
{
    public bool TechnicalOfflineEnabled => !ReadOnlyMode && bool.TryParse(configuration["TechnicalOfflineEnabled"], out var enabled) && enabled;
    private readonly SemaphoreSlim technicalSyncGate = new(1, 1);
    private readonly Dictionary<Guid, OrdemServicoDetalheDto> technicalDetails = [];
    private readonly HashSet<Guid> pendingOrders = [];
    private readonly SemaphoreSlim prepareGate = new(1, 1);
    private DateTimeOffset lastPreparationAttempt;
    private Task? preparationTask;
    public DateTimeOffset? PreparedAt { get; private set; }
    public string PreparationNotice { get; private set; } = "Preparação automática aguardando conexão.";
    public void PrepareInBackground(bool force = false)
    {
        if (preparationTask is { IsCompleted: false }) return;
        preparationTask = PrepareAutomatically(force);
    }
    public async Task ReconnectAsync()
    {
        try { await SyncAsync(); PrepareInBackground(force: true); }
        catch (Exception ex) { Notice = $"Pendências preservadas. {ex.Message}"; Changed?.Invoke(); }
    }
    private async Task PrepareAutomatically(bool force)
    {
        if (!TechnicalOfflineEnabled || !Role("Tecnico") || stop.IsCancellationRequested ||
            (!force && DateTimeOffset.UtcNow - lastPreparationAttempt < TimeSpan.FromMinutes(1)) || !await prepareGate.WaitAsync(0)) return;
        var session = Session;
        try
        {
            if (!await js.InvokeAsync<bool>("nexStorage.isOnline"))
            { PreparationNotice = PreparedAt.HasValue ? "Sem conexão. Usando os atendimentos salvos neste dispositivo." : "Conecte-se para baixar os atendimentos automaticamente."; return; }
            lastPreparationAttempt = DateTimeOffset.UtcNow;
            PreparationNotice = "Preparando seus atendimentos automaticamente…"; Changed?.Invoke();
            void CheckSession()
            {
                if (!ReferenceEquals(Session, session) || stop.IsCancellationRequested) throw new OperationCanceledException();
            }
            async Task<T> Download<T>(string path)
            {
                CheckSession();
                var value = await Send<T>(HttpMethod.Get, path);
                await Locked(async () =>
                {
                    CheckSession();
                    await Write("local", Key("cache:" + path), new CacheEntry<T>(DateTimeOffset.UtcNow, value));
                    return true;
                });
                return value;
            }
            var orders = await Download<List<OrdemServicoDto>>("api/ordensservico?minhas=true");
            await Download<List<EquipamentoDto>>("api/equipamentos");
            var stockRevision = await Locked(async () => (await Technical()).Revision);
            var stock = await Download<List<PecaDto>>("api/pecas");
            await Locked(async () =>
            {
                CheckSession();
                var state = await Technical();
                if (state.Revision == stockRevision && !state.Events.Any(e => e.Acao == "pecas"))
                { state.Stock = stock; await SaveTechnical(state); }
                return true;
            });
            await Download<List<CausaFalhaDto>>("api/causas-falha");
            await Download<List<PlanoPreventivoDto>>("api/planos-preventivos");
            foreach (var order in orders.Where(o => o.Status is not ("Concluida" or "Cancelada")))
            {
                CheckSession();
                var revision = await Locked(async () => (await Technical()).Revision);
                var detail = await Send<OrdemServicoDetalheDto>(HttpMethod.Get, $"api/ordensservico/{order.Id}");
                await Locked(async () =>
                {
                    CheckSession();
                    var state = await Technical();
                    if (state.Revision == revision && !state.Events.Any(e => e.OrdemServicoId == order.Id))
                    { state.Details.RemoveAll(d => d.Id == order.Id); state.Details.Add(detail); await SaveTechnical(state); }
                    return true;
                });
            }
            await Locked(async () =>
            {
                CheckSession();
                var state = await Technical();
                state.Details.RemoveAll(d => orders.All(o => o.Id != d.Id) && state.Events.All(e => e.OrdemServicoId != d.Id));
                if (orders.Any(o => o.Status is not ("Concluida" or "Cancelada") && state.Details.All(d => d.Id != o.Id)))
                    throw new InvalidOperationException("Preparação incompleta; uma nova tentativa ocorrerá automaticamente.");
                state.PreparedAt = DateTimeOffset.UtcNow;
                await SaveTechnical(state);
                PreparationNotice = "Seus atendimentos estão salvos para uso offline. Atualização automática ativa.";
                return true;
            });
        }
        catch (OperationCanceledException) when (!ReferenceEquals(Session, session) || stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(Session, session)) PreparationNotice = $"Não foi possível concluir a preparação automática. Dados e pendências anteriores preservados. {ex.Message}";
        }
        finally { prepareGate.Release(); Changed?.Invoke(); }
    }
    private Task<TechnicalState?> ReadTechnical() => Read<TechnicalState>("local", Key("technical"));
    private async Task<TechnicalState> Technical()
    {
        var state = await ReadTechnical() ?? new();
        // Recupera filas bloqueadas pela versão anterior ao receber um erro temporário da API.
        if (state.Blocked && new[] { "API 408:", "API 429:", "API 500:", "API 502:", "API 503:", "API 504:" }.Any(state.Error.Contains))
        {
            state.Blocked = false;
            state.Error = "Serviço temporariamente indisponível. Pendências preservadas para nova tentativa.";
        }
        return state;
    }
    public bool HasPending(Guid id) => pendingOrders.Contains(id);
    public OrdemServicoDetalheDto? LocalDetail(Guid id) => technicalDetails.GetValueOrDefault(id);
    private async Task UpdatePending()
    {
        var state = TechnicalOfflineEnabled ? await Technical() : new();
        pendingOrders.Clear();
        foreach (var e in state.Events) pendingOrders.Add(e.OrdemServicoId);
        technicalDetails.Clear();
        foreach (var d in state.Details) technicalDetails[d.Id] = d;
        Pending = (await Queue()).Count + state.Events.Count;
        PreparedAt = state.PreparedAt;
        if (state.Blocked) Notice = state.Error;
    }
    private async Task SaveTechnical(TechnicalState state)
    {
        // Fila e visão local mudam num único setItem: falha de armazenamento não confirma a ação.
        state.Revision++;
        await Write("local", Key("technical"), state);
        await UpdatePending();
    }
    public Task<List<OrdemServicoDto>> OverlayTechnicalAsync(List<OrdemServicoDto> orders) => Locked(async () =>
    {
        await UpdatePending();
        foreach (var order in orders)
            if (HasPending(order.Id) && LocalDetail(order.Id) is { } d)
            { order.Status = d.Status; order.DataConclusao = d.DataConclusao; }
        foreach (var d in technicalDetails.Values.Where(d => HasPending(d.Id) && orders.All(o => o.Id != d.Id)))
            orders.Add(new OrdemServicoDto { Id = d.Id, NumeroOS = d.NumeroOS, Status = d.Status, Prioridade = d.Prioridade,
                EquipamentoNome = d.EquipamentoNome, TecnicoNome = d.TecnicoNome, DescricaoProblema = d.DescricaoProblema,
                DataAbertura = d.DataAbertura, DataConclusao = d.DataConclusao });
        return orders;
    });
    public Task<List<PecaDto>> TechnicalStockAsync(List<PecaDto> stock) => Locked(async () =>
    {
        var state = await Technical();
        if (state.Events.Any(e => e.Acao == "pecas") && state.Stock is not null) return state.Stock;
        state.Stock = stock;
        await SaveTechnical(state);
        return stock;
    });
    public Task<OrdemServicoDetalheDto> TechnicalDetailAsync(Guid id, bool requireOnline = false) => Locked(async () =>
    {
        var state = await Technical();
        var local = state.Details.FirstOrDefault(d => d.Id == id);
        if (state.Events.Any(e => e.OrdemServicoId == id)) return local!;
        try
        {
            var detail = await Send<OrdemServicoDetalheDto>(HttpMethod.Get, $"api/ordensservico/{id}");
            state.Details.RemoveAll(d => d.Id == id); state.Details.Add(detail);
            await SaveTechnical(state);
            return detail;
        }
        catch (Exception ex) when (!requireOnline && (ex is HttpRequestException or TaskCanceledException))
        {
            return local ?? throw new InvalidOperationException("Prepare esta ordem enquanto estiver conectado antes de trabalhar offline.");
        }
    });
    public async Task<OrdemServicoDetalheDto> RecordTechnicalAsync(Guid id, string action, object body)
    {
        var result = await Locked(async () =>
        {
            if (!TechnicalOfflineEnabled || !Role("Tecnico")) throw new InvalidOperationException("Atendimento offline indisponível para este perfil.");
            var state = await Technical();
            if (state.Blocked) throw new InvalidOperationException("Há uma pendência recusada pelo servidor. Revise a mensagem e procure o gestor antes de continuar.");
            var detail = state.Details.FirstOrDefault(d => d.Id == id) ?? throw new InvalidOperationException("Prepare a ordem para atendimento offline.");
            var e = new EventoAtendimentoDto { Id = Guid.NewGuid(), OrdemServicoId = id, Acao = action, OcorridoEm = DateTimeOffset.UtcNow };
            switch (body)
            {
                case PausarOrdemServicoDto p: e.Motivo = p.Motivo; break;
                case RegistrarPecaDto p: e.PecaId = p.PecaId; e.Quantidade = p.Quantidade; break;
                case ConcluirOrdemServicoDto c: e.CausaFalhaId = c.CausaFalhaId; e.Observacoes = c.ObservacoesTecnicas; break;
            }
            var updated = Project(detail, e, state.Stock ?? []);
            state.Details.RemoveAll(d => d.Id == id); state.Details.Add(updated); state.Events.Add(e);
            await SaveTechnical(state);
            Notice = "Atendimento salvo neste dispositivo; aguardando confirmação da sincronização.";
            return updated;
        });
        _ = SyncTechnicalSafe();
        return result;
    }
    private async Task SyncTechnicalSafe()
    {
        if (!TechnicalOfflineEnabled || !Role("Tecnico") || !await technicalSyncGate.WaitAsync(0)) return;
        var email = Session!.Email;
        try
        {
            if (!await js.InvokeAsync<bool>("nexStorage.isOnline")) return;
            while (Session?.Email == email && Role("Tecnico"))
            {
                var next = await Locked(async () => (await Technical()).Events.FirstOrDefault());
                if (next is null) return;
                try
                {
                    var confirmed = await Send<OrdemServicoDetalheDto>(HttpMethod.Post, $"api/ordensservico/{next.OrdemServicoId}/eventos", next);
                    await Locked(async () =>
                    {
                        if (Session?.Email != email) return false;
                        var state = await Technical();
                        if (!state.Events.Any(e => e.Id == next.Id)) return false;
                        state.Events.RemoveAll(e => e.Id == next.Id);
                        // Reaplica os eventos posteriores à resposta confirmada, sem consumir estoque duas vezes.
                        var scratch = (state.Stock ?? []).Select(p => new PecaDto { Id = p.Id, Nome = p.Nome, QuantidadeEstoque = int.MaxValue }).ToList();
                        foreach (var e in state.Events.Where(e => e.OrdemServicoId == next.OrdemServicoId)) confirmed = Project(confirmed, e, scratch);
                        state.Details.RemoveAll(d => d.Id == confirmed.Id); state.Details.Add(confirmed);
                        state.Blocked = false;
                        state.Error = "";
                        await SaveTechnical(state);
                        Notice = state.Events.Count == 0 ? "Atendimentos sincronizados." : "Sincronizando atendimentos…";
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    if (Session?.Email != email) return;
                    await Locked(async () =>
                    {
                        var state = await Technical();
                        state.Blocked = ex is InvalidOperationException;
                        state.Error = ex is HttpRequestException or TaskCanceledException
                            ? $"Sincronização aguardando conexão com o servidor. {state.Events.Count} evento(s) salvo(s) no dispositivo. A tentativa será repetida automaticamente; não é necessário registrar o atendimento novamente."
                            : $"Pendências de atendimento preservadas. {ex.Message}";
                        await SaveTechnical(state);
                        Notice = state.Error;
                        return true;
                    });
                    return;
                }
            }
        }
        catch (Exception ex) { Notice = $"Não foi possível sincronizar. Pendências preservadas. {ex.Message}"; Changed?.Invoke(); }
        finally { technicalSyncGate.Release(); }
    }
    private static OrdemServicoDetalheDto Project(OrdemServicoDetalheDto source, EventoAtendimentoDto e, List<PecaDto> stock)
    {
        var d = JsonSerializer.Deserialize<OrdemServicoDetalheDto>(JsonSerializer.Serialize(source))!;
        if (d.TecnicoId is not Guid technician) throw new InvalidOperationException("Ordem sem técnico responsável.");
        var os = new OrdemServico { Id = d.Id, TecnicoId = technician, Status = Enum.Parse<StatusOS>(d.Status), DataAbertura = d.DataAbertura,
            RegistrosTempo = d.Registros.Select(r => new OsRegistroTempo { Timestamp = r.Timestamp, TipoRegistro = Enum.Parse<TipoRegistroTempo>(r.Tipo), MotivoPausa = r.MotivoPausa }).ToList() };
        if (e.OcorridoEm < d.DataAbertura || d.Registros.Any(r => r.Timestamp > e.OcorridoEm)) throw new InvalidOperationException("Relógio do dispositivo anterior ao último registro da ordem.");
        switch (e.Acao)
        {
            case "iniciar": os.Iniciar(technician, e.OcorridoEm); break;
            case "pausar": os.Pausar(technician, e.Motivo ?? "", e.OcorridoEm); break;
            case "retomar": os.Retomar(technician, e.OcorridoEm); break;
            case "concluir":
                if (e.CausaFalhaId is null || e.CausaFalhaId == Guid.Empty) throw new InvalidOperationException("Selecione a causa da falha.");
                os.Concluir(technician, new CausaFalha { Id = e.CausaFalhaId.Value, Ativo = true }, e.Observacoes, e.OcorridoEm);
                d.DataConclusao = e.OcorridoEm; d.ObservacoesTecnicas = e.Observacoes;
                break;
            case "pecas":
                var p = stock.FirstOrDefault(p => p.Id == e.PecaId) ?? throw new InvalidOperationException("Peça não disponível no estoque salvo.");
                os.RegistrarPeca(technician, new Peca { Id = p.Id, Nome = p.Nome, QuantidadeEstoque = p.QuantidadeEstoque }, e.Quantidade);
                p.QuantidadeEstoque -= e.Quantidade;
                var used = d.Pecas.FirstOrDefault(p => p.PecaId == e.PecaId);
                if (used is null) d.Pecas.Add(new PecaUtilizadaDto { PecaId = p.Id, Nome = p.Nome, Codigo = p.Codigo, Quantidade = e.Quantidade });
                else used.Quantidade += e.Quantidade;
                break;
            default: throw new InvalidOperationException("Ação de atendimento inválida.");
        }
        d.Status = os.Status.ToString();
        d.Registros = os.RegistrosTempo.Select(r => new RegistroTempoDto { Tipo = r.TipoRegistro.ToString(), Timestamp = r.Timestamp, MotivoPausa = r.MotivoPausa }).ToList();
        d.TempoAtendimentoMinutos = os.CalcularTempoAtendimento(e.OcorridoEm).TotalMinutes;
        return d;
    }
    private sealed class TechnicalState
    {
        public List<EventoAtendimentoDto> Events { get; set; } = [];
        public List<OrdemServicoDetalheDto> Details { get; set; } = [];
        public List<PecaDto>? Stock { get; set; }
        public bool Blocked { get; set; }
        public string Error { get; set; } = "";
        public DateTimeOffset? PreparedAt { get; set; }
        public long Revision { get; set; }
    }
}
