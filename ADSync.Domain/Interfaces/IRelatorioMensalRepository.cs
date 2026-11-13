using ADSync.Domain.Entities;

namespace ADSync.Domain.Interfaces;

public interface IRelatorioMensalRepository
{
    Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo);
    Task<int> ContarAberturasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo);
    Task<List<OrdemServico>> ListarPendentesAsync(DateTimeOffset agora);
    Task<List<PlanoPreventivo>> ListarPlanosAtivosAsync();
    Task<List<Peca>> ListarPecasCriticasSemEstoqueAsync();
}
