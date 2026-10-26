using ADSync.Domain.Entities;

namespace ADSync.Domain.Interfaces;

public interface IPlanoPreventivoRepository
{
    Task<PlanoPreventivo?> ObterPorIdAsync(Guid id);

    Task<List<PlanoPreventivo>> ListarAsync(bool incluirInativos, Guid? equipamentoId);

    // Planos ativos cuja data chegou e que não têm nenhuma O.S. ainda em aberto
    // (Aberta, EmAndamento ou Pausada). Usado pelo job de geração automática.
    Task<List<PlanoPreventivo>> ListarPendentesAsync(DateOnly referencia);

    Task AdicionarAsync(PlanoPreventivo plano);
    Task<bool> SalvarAlteracoesAsync();
}