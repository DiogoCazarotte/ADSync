using ADSync.Domain.Exceptions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ADSync.Infrastructure.Safety;

/// <summary>Blocks EF writes, including Identity lockout/reset and password rehash.</summary>
public sealed class ReadOnlySaveChangesInterceptor(bool enabled) : SaveChangesInterceptor
{
    private void Guard()
    {
        if (enabled)
            throw new AcessoNegadoException("Modo somente leitura: atualização de dados ou credenciais bloqueada.");
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard();
        return ValueTask.FromResult(result);
    }
}
