using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

public class PlanoPreventivoRepository : IPlanoPreventivoRepository
{
    private readonly AdSyncDbContext _context;
    public PlanoPreventivoRepository(AdSyncDbContext context) => _context = context;

    public Task<PlanoPreventivo?> ObterPorIdAsync(Guid id) =>
        _context.PlanosPreventivos
            .Include(p => p.Equipamento)
            .Include(p => p.TecnicoResponsavel)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<PlanoPreventivo>> ListarAsync(bool incluirInativos, Guid? equipamentoId)
    {
        var query = _context.PlanosPreventivos
            .AsNoTracking()
            .Include(p => p.Equipamento)
            .Include(p => p.TecnicoResponsavel)
            .AsQueryable();

        if (!incluirInativos)
            query = query.Where(p => p.Ativo);

        if (equipamentoId is not null)
            query = query.Where(p => p.EquipamentoId == equipamentoId);

        return await query.OrderBy(p => p.ProximaExecucao).ToListAsync();
    }

    public async Task<List<PlanoPreventivo>> ListarPendentesAsync(DateOnly referencia)
    {
        // Só gera a próxima O.S. quando a anterior deste plano já foi concluída ou cancelada,
        // para não empilhar preventivas do mesmo plano enquanto uma ainda está em aberto.
        return await _context.PlanosPreventivos
            .Include(p => p.Equipamento)
            .Include(p => p.TecnicoResponsavel)
            .Where(p => p.Ativo
                && p.Equipamento!.Ativo
                && p.TecnicoResponsavel!.Ativo
                && p.ProximaExecucao <= referencia
                && !_context.OrdensServico.Any(os =>
                    os.PlanoPreventivoId == p.Id &&
                    (os.Status == StatusOS.Aberta || os.Status == StatusOS.EmAndamento || os.Status == StatusOS.Pausada)))
            .ToListAsync();
    }

    public async Task AdicionarAsync(PlanoPreventivo plano) => await _context.PlanosPreventivos.AddAsync(plano);

    public async Task<bool> SalvarAlteracoesAsync() => await _context.SaveChangesAsync() > 0;
}