using ADSync.Domain.Entities;

namespace ADSync.Domain.Interfaces;

public interface IIndicadoresRepository
{
    // Só O.S. Concluídas, com DataConclusao dentro de [inicio, fimExclusivo).
    // Vem com Equipamento (para o setor) e RegistrosTempo (para o tempo de atendimento).
    Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo);
    bool MtbfHabilitado { get; }
    Task<List<Equipamento>> ListarEquipamentosMtbfAsync();
    Task<List<OperacaoDiaria>> ListarOperacoesAsync(DateOnly inicio, DateOnly fim);
    Task<List<OrdemServico>> ListarFalhasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo);
}
