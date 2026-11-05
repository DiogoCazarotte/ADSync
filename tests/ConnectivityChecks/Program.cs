using System.Data.Common;
using System.Net.Sockets;
using System.Security.Claims;
using ADSync.Api.Auth;
using ADSync.Api.Middleware;
using ADSync.Domain.Enums;
using ADSync.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.NameTranslation;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
Exception Failure() => new InvalidOperationException("Likely due to a transient failure", new NpgsqlException("Exception while reading from stream",
    new IOException("Remote host disconnected", new SocketException((int)SocketError.ConnectionReset))));
Check(DatabaseAvailability.IsUnavailable(Failure()), "Falha transitória encapsulada não identificada.");
Check(!DatabaseAvailability.IsUnavailable(new InvalidOperationException("Erro de programação")), "Erro de programação confundido com rede.");
var handler = new DomainExceptionHandler();
var http = new DefaultHttpContext(); http.Response.Body = new MemoryStream();
Check(await handler.TryHandleAsync(http, Failure(), default), "Falha não tratada.");
Check(http.Response.StatusCode == 503 && http.Response.Headers.RetryAfter == "20", "Status/tempo de nova tentativa incorretos.");
http.Response.Body.Position = 0;
var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
Check(!body.Contains("Npgsql") && !body.Contains("stream") && body.Contains("pendentes"), "Resposta expôs detalhes ou não orientou preservar pendências.");
Console.WriteLine("PASS: perda de conexão com PostgreSQL vira 503 com nova tentativa e mensagem segura.");

// O interceptor falha ANTES de abrir a conexão. Nenhuma rede ou banco é utilizado.
var fault = new OfflineConnection();
var services = new ServiceCollection(); services.AddLogging();
services.AddDbContext<AdSyncDbContext>(options => options.UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused", o =>
{
    var translator = new NpgsqlNullNameTranslator();
    o.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: translator);
    o.MapEnum<StatusOS>("status_os_enum", nameTranslator: translator);
    o.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: translator);
    o.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: translator);
    o.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: translator);
}).AddInterceptors(fault));
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
context.Response.Body = new MemoryStream();
var scheme = new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler));
var options = new JwtBearerOptions();
var validation = new TokenValidatedContext(context, scheme, options)
{
    Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", "Tecnico")], "test"))
};
await AccountTokenValidation.ValidateAsync(validation);
Check(validation.Result?.Failure is not null && fault.Attempts == 1, "Conta foi aceita sem consultar o banco.");
var challenge = new JwtBearerChallengeContext(context, scheme, options, new AuthenticationProperties());
await AccountTokenValidation.ChallengeAsync(challenge);
Check(context.Response.StatusCode == 503 && challenge.Handled, "Banco offline foi tratado como sessão inválida em vez de indisponibilidade.");
var invalid = new TokenValidatedContext(new DefaultHttpContext(), scheme, options) { Principal = new ClaimsPrincipal(new ClaimsIdentity()) };
await AccountTokenValidation.ValidateAsync(invalid);
Check(invalid.Result?.Failure is not null && fault.Attempts == 1, "Token sem identificador não foi recusado.");
Console.WriteLine("PASS: autenticação com banco offline não libera acesso e responde 503; token inválido continua recusado.");

using var canceled = new CancellationTokenSource(); canceled.Cancel();
var canceledHttp = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = canceled.Token };
var canceledStream = new DisconnectingStream(canceled, true); canceledHttp.Response.Body = canceledStream;
canceledHttp.Items[DatabaseAvailability.UnavailableKey] = true;
var canceledValidation = new TokenValidatedContext(canceledHttp, scheme, options) { Principal = validation.Principal };
await AccountTokenValidation.ValidateAsync(canceledValidation);
Check(canceledValidation.Result?.Failure is not null && fault.Attempts == 1, "Requisição cancelada consultou o banco ou liberou acesso.");
var canceledChallenge = new JwtBearerChallengeContext(canceledHttp, scheme, options, new AuthenticationProperties());
await AccountTokenValidation.ChallengeAsync(canceledChallenge);
Check(canceledChallenge.Handled && canceledStream.Attempts == 0, "Challenge tentou escrever após cancelamento.");
await handler.TryHandleAsync(canceledHttp, Failure(), canceled.Token);
Check(canceledStream.Attempts == 0, "Tratamento de exceção tentou responder à conexão cancelada.");
Console.WriteLine("PASS: requisição já cancelada não consulta o banco nem tenta escrever uma resposta.");

foreach (var ioFailure in new[] { false, true })
{
    using var disconnect = new CancellationTokenSource();
    var raceHttp = new DefaultHttpContext { RequestAborted = disconnect.Token };
    var raceStream = new DisconnectingStream(disconnect, true, ioFailure); raceHttp.Response.Body = raceStream;
    raceHttp.Items[DatabaseAvailability.UnavailableKey] = true;
    var raceChallenge = new JwtBearerChallengeContext(raceHttp, scheme, options, new AuthenticationProperties());
    await AccountTokenValidation.ChallengeAsync(raceChallenge);
    Check(raceChallenge.Handled && raceStream.Attempts > 0 && disconnect.IsCancellationRequested, "Cancelamento durante o flush não foi exercitado.");
}
Console.WriteLine("PASS: desconexão durante o envio do JSON encerra o challenge sem propagar cancelamento/erro de transporte.");

using var active = new CancellationTokenSource();
var activeHttp = new DefaultHttpContext { RequestAborted = active.Token };
activeHttp.Response.Body = new DisconnectingStream(active, false);
try
{
    await DatabaseAvailability.WriteResponseAsync(activeHttp, active.Token);
    throw new Exception("Cancelamento inesperado foi escondido.");
}
catch (OperationCanceledException) { }
Console.WriteLine("PASS: cancelamentos sem relação com a desconexão continuam sendo propagados.");

sealed class OfflineConnection : DbConnectionInterceptor
{
    public int Attempts { get; private set; }
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new NpgsqlException("Exception while reading from stream", new IOException("Transport disconnected", new SocketException((int)SocketError.ConnectionReset)));
    }
}

sealed class DisconnectingStream(CancellationTokenSource source, bool cancel, bool ioFailure = false) : MemoryStream
{
    public int Attempts { get; private set; }
    private Exception Failure()
    {
        Attempts++;
        if (cancel) source.Cancel();
        return ioFailure ? new IOException("Conexão encerrada no envio.") : new OperationCanceledException("Flush cancelado.", source.Token);
    }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(Failure());
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(Failure());
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(Failure());
}
