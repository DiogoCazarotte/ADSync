using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ADSync.Infrastructure.Repositories;

public class IndicadoresRepository : IIndicadoresRepository
{
    private readonly AdSyncDbContext _context;
    public bool MtbfHabilitado { get; }
    public IndicadoresRepository(AdSyncDbContext context, IConfiguration configuration)
    {
        _context = context;
        MtbfHabilitado = configuration.GetValue<bool>("Mtbf:Enabled");
    }

    public Task<List<OperacaoDiaria>> ListarOperacoesAsync(DateOnly inicio, DateOnly fim) =>
        _context.OperacoesDiarias.AsNoTracking().Include(o => o.Equipamento)
            .Where(o => o.Data >= inicio && o.Data <= fim).ToListAsync();

    public Task<List<Equipamento>> ListarEquipamentosMtbfAsync() => _context.Equipamentos.AsNoTracking().ToListAsync();

    public Task<List<OrdemServico>> ListarFalhasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo) =>
        _context.OrdensServico.AsNoTracking().Include(o => o.Equipamento)
            .Where(o => o.TipoManutencao == TipoManutencao.Corretiva && o.Status != StatusOS.Cancelada
                && o.DataAbertura >= inicio && o.DataAbertura < fimExclusivo).ToListAsync();

    public async Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fimExclusivo)
    {
        return await _context.OrdensServico
            .AsNoTracking()
            .Include(os => os.Equipamento)
            .Include(os => os.RegistrosTempo)
            .Where(os => os.Status == StatusOS.Concluida
                && os.DataConclusao != null
                && os.DataConclusao >= inicio
                && os.DataConclusao < fimExclusivo)
            .ToListAsync();
    }
}
