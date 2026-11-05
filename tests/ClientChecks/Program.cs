using ADSync.Client.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

await using var client = new ManutencaoClient(new HttpClient(), new Storage(), new ConfigurationBuilder().Build());
Action brokenSubscriber = () => throw new InvalidOperationException("render failure");
client.Changed += brokenSubscriber;
try { await client.InitializeAsync(); }
catch (InvalidOperationException) { }
client.Changed -= brokenSubscriber;
if (client.Busy) throw new Exception("Busy permaneceu ativo após falha de notificação.");
await client.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(2));
if (client.Busy) throw new Exception("O bloqueio não foi liberado.");
Console.WriteLine("PASS: falha na notificação libera o bloqueio e permite a próxima operação.");

var storage = new Storage();
var scopedConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["StorageScope"] = "supabase" }).Build();
await using var scoped = new ManutencaoClient(new HttpClient(), storage, scopedConfig);
await scoped.InitializeAsync();
if (!storage.Keys.SequenceEqual(new[] { "nexservice:supabase:session" }))
    throw new Exception("Sessão do Supabase acessou armazenamento antigo.");
Console.WriteLine("PASS: inicialização do Supabase não reutiliza sessão ou fila da demonstração.");

sealed class Storage : IJSRuntime
{
    public List<string> Keys { get; } = [];
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier == "nexStorage.get") Keys.Add((string)args![1]!);
        return ValueTask.FromResult(default(TValue)!);
    }
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
}
