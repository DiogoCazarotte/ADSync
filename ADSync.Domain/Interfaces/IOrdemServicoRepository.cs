using ADSync.Domain.Entities;
using ADSync.Domain.Enums;

namespace ADSync.Domain.Interfaces;

public interface IOrdemServicoRepository
{
    Task<OrdemServico?> ObterPorIdAsync(Guid id);
    Task<OrdemServico?> ObterPorClientSyncIdAsync(Guid clientSyncId);
    Task<List<OrdemServico>> ListarAsync(StatusOS? status = null, PrioridadeOS? prioridade = null);
    Task AdicionarAsync(OrdemServico ordemServico);
    Task<bool> SalvarAlteracoesAsync();
}
