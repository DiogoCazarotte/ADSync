using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

public class OrdemServicoRepository : IOrdemServicoRepository
{
    private readonly AdSyncDbContext _context;

    public OrdemServicoRepository(AdSyncDbContext context)
    {
        _context = context;
    }

    public async Task<OrdemServico?> ObterPorIdAsync(Guid id)
    {
        return await _context.OrdensServico
            .Include(os => os.Equipamento)
            .Include(os => os.Operador)
            .Include(os => os.Tecnico)
            .Include(os => os.CausaFalha)
            .FirstOrDefaultAsync(os => os.Id == id);
    }

    public async Task<OrdemServico?> ObterDetalhadoAsync(Guid id)
    {
        return await _context.OrdensServico
            .Include(os => os.Equipamento)
            .Include(os => os.Operador)
            .Include(os => os.Tecnico)
            .Include(os => os.CausaFalha)
            .Include(os => os.RegistrosTempo)
            .Include(os => os.PecasUtilizadas).ThenInclude(p => p.Peca)
            .AsSplitQuery() // evita juntar tudo numa consulta gigante com várias coleções
            .FirstOrDefaultAsync(os => os.Id == id);
    }

    // Usado para idempotência: evita criar uma O.S. duplicada se o técnico
    // sincronizar o mesmo registro offline mais de uma vez.
    public async Task<OrdemServico?> ObterPorClientSyncIdAsync(Guid clientSyncId)
    {
        return await _context.OrdensServico
            .Include(os => os.Equipamento)
            .Include(os => os.Operador)
            .Include(os => os.Tecnico)
            .FirstOrDefaultAsync(os => os.ClientSyncId == clientSyncId);
    }

    public async Task<List<OrdemServico>> ListarAsync(
        StatusOS? status = null,
        PrioridadeOS? prioridade = null,
        Guid? tecnicoId = null,
        Guid? operadorId = null)
    {
        var query = _context.OrdensServico
            .AsNoTracking()
            .Include(os => os.Equipamento)
            .Include(os => os.Operador)
            .Include(os => os.Tecnico)
            .AsQueryable();

        if (status is not null)
            query = query.Where(os => os.Status == status);

        if (prioridade is not null)
            query = query.Where(os => os.Prioridade == prioridade);

        if (tecnicoId is not null)
            query = query.Where(os => os.TecnicoId == tecnicoId);

        if (operadorId is not null)
            query = query.Where(os => os.OperadorId == operadorId);

        return await query.OrderByDescending(os => os.DataAbertura).ToListAsync();
    }

    public async Task AdicionarAsync(OrdemServico ordemServico)
    {
        await _context.OrdensServico.AddAsync(ordemServico);
    }

    public async Task<bool> SalvarAlteracoesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }
}