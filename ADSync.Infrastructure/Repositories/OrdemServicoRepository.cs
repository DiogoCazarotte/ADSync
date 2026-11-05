using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Domain.Exceptions;
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

    public async Task<string> ObterProximoNumeroAsync()
    {
        // A sequence os_numero_seq é criada no Supabase (ver scripts/passo2.sql).
        var proximo = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('os_numero_seq') AS \"Value\"")
            .SingleAsync();

        return $"OS-{DateTime.UtcNow.Year}-{proximo:D5}";
    }

    public async Task AdicionarAsync(OrdemServico ordemServico)
    {
        await _context.OrdensServico.AddAsync(ordemServico);
    }

    public async Task<OrdemServico> AdicionarComAtribuicaoAsync(OrdemServico ordemServico)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        // Trava transacional compartilhada pelas aberturas desta aplicação. A próxima
        // abertura considera a carga já gravada pela anterior, inclusive entre instâncias.
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1094996825)");

        if (ordemServico.ClientSyncId is Guid syncId)
        {
            var existente = await ObterPorClientSyncIdAsync(syncId);
            if (existente is not null)
            {
                if (existente.OperadorId != ordemServico.OperadorId)
                    throw new AcessoNegadoException("Identificador de sincronização pertence a outra conta.");
                await tx.CommitAsync();
                return existente;
            }
        }

        var tecnico = await _context.Usuarios
            .Where(u => u.Ativo && u.TipoUsuario == TipoUsuario.Tecnico)
            .OrderBy(u => u.OrdensComoTecnico.Count(os => os.Status == StatusOS.Aberta ||
                os.Status == StatusOS.EmAndamento || os.Status == StatusOS.Pausada))
            // Empate: quem não recebeu ordens ou recebeu há mais tempo; depois o Id.
            .ThenBy(u => u.OrdensComoTecnico.Any() ? 1 : 0)
            .ThenBy(u => u.OrdensComoTecnico.Max(os => (DateTimeOffset?)os.DataAbertura))
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync();

        if (tecnico is not null) ordemServico.AtribuirTecnico(tecnico);
        await _context.OrdensServico.AddAsync(ordemServico);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return ordemServico;
    }

    public async Task<bool> SalvarAlteracoesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }
}
