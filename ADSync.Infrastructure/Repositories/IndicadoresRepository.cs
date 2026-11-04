using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

public class IndicadoresRepository : IIndicadoresRepository
{
    private readonly AdSyncDbContext _context;
    public IndicadoresRepository(AdSyncDbContext context) => _context = context;

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