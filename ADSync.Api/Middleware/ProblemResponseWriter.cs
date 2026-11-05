using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Middleware;

public static class ProblemResponseWriter
{
    public static async Task WriteAsync(HttpContext context, ProblemDetails problem, CancellationToken cancellationToken = default)
    {
        if (context.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested || context.Response.HasStarted) return;
        try
        {
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            // O cliente pode desconectar entre a verificação acima e o flush do JSON.
        }
        catch (IOException) when (context.RequestAborted.IsCancellationRequested)
        {
            // A conexão encerrada não pode receber uma segunda resposta de erro.
        }
    }
}
