using ADSync.Domain.Entities;
using ADSync.Domain.Enums;

namespace ADSync.Domain.Interfaces;

public interface IOrdemServicoRepository
{
    // Com Equipamento, Operador, Tecnico e CausaFalha
    Task<OrdemServico?> ObterPorIdAsync(Guid id);

    // Com tudo acima + RegistrosTempo e PecasUtilizadas (com a Peca). Usado nas ações do fluxo.
    Task<OrdemServico?> ObterDetalhadoAsync(Guid id);

    Task<OrdemServico?> ObterPorClientSyncIdAsync(Guid clientSyncId);

    Task<List<OrdemServico>> ListarAsync(
        StatusOS? status = null,
        PrioridadeOS? prioridade = null,
        Guid? tecnicoId = null,
        Guid? operadorId = null);

    Task AdicionarAsync(OrdemServico ordemServico);

    // Grava TUDO que está rastreado no contexto de uma vez só, numa única transação.
    // É por isso que a baixa de estoque e o registro da peça na O.S. são atômicos.
    Task<bool> SalvarAlteracoesAsync();
}