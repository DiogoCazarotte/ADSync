using ADSync.Domain.Entities;

namespace ADSync.Domain.Interfaces;

public interface IIndicadoresRepository
{
    // Só O.S. Concluídas, com DataConclusao dentro de [inicio, fimExclusivo).
    // Vem com Equipamento (para o setor) e RegistrosTempo (para o tempo de atendimento).
    Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo);
}