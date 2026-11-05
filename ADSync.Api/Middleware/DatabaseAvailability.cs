using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ADSync.Api.Middleware;

public static class DatabaseAvailability
{
    public const string UnavailableKey = "ADSync.DatabaseUnavailable";
    public static bool IsUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is NpgsqlException { IsTransient: true }) return true;
        return false;
    }
    public static Task WriteResponseAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested || context.Response.HasStarted) return Task.CompletedTask;
        context.Response.Headers.RetryAfter = "20";
        return ProblemResponseWriter.WriteAsync(context, new ProblemDetails
        {
            Status = 503,
            Title = "Serviço temporariamente indisponível",
            Detail = "Sem conexão com o banco de dados. Os registros locais devem permanecer pendentes até a conexão voltar."
        }, cancellationToken);
    }
}
