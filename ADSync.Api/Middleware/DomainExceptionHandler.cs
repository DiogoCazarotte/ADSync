using ADSync.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Api.Middleware;

// Traduz as exceções de negócio do Domain em respostas HTTP claras,
// em vez de deixar virar um "500 Internal Server Error".
public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            NaoEncontradoException => (StatusCodes.Status404NotFound, "Não encontrado"),
            AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            RegraNegocioException => (StatusCodes.Status409Conflict, "Regra de negócio violada"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de concorrência"),
            _ => (0, string.Empty)
        };

        // Qualquer outra exceção segue o caminho normal (500)
        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = exception is DbUpdateConcurrencyException
                ? "Outro usuário alterou este registro ao mesmo tempo. Atualize e tente novamente."
                : exception.Message
        }, cancellationToken);

        return true;
    }
}