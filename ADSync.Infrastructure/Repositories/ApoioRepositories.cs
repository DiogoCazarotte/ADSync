using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

// Os registros buscados por Id voltam RASTREADOS pelo contexto de propósito:
// quando o service altera a entidade, o SaveChanges grava a mudança.
// As listagens usam AsNoTracking (só leitura, mais leve).

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AdSyncDbContext _context;
    public UsuarioRepository(AdSyncDbContext context) => _context = context;

    public Task<Usuario?> ObterPorIdAsync(Guid id) =>
        _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<Usuario>> ListarAsync(TipoUsuario? tipo, bool incluirInativos)
    {
        var query = _context.Usuarios.AsNoTracking().AsQueryable();

        if (tipo is not null)
            query = query.Where(u => u.TipoUsuario == tipo);

        if (!incluirInativos)
            query = query.Where(u => u.Ativo);

        return await query.OrderBy(u => u.Nome).ToListAsync();
    }
}

public class PecaRepository : IPecaRepository
{
    private readonly AdSyncDbContext _context;
    public PecaRepository(AdSyncDbContext context) => _context = context;

    public Task<Peca?> ObterPorIdAsync(Guid id) =>
        _context.Pecas.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Peca>> ListarAsync(string? busca)
    {
        var query = _context.Pecas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim().ToLower();
            query = query.Where(p => p.Nome.ToLower().Contains(termo) || p.Codigo.ToLower().Contains(termo));
        }

        return await query.OrderBy(p => p.Nome).ToListAsync();
    }

    public Task<bool> CodigoExisteAsync(string codigo, Guid? ignorarId = null)
    {
        var c = codigo.Trim().ToLower();
        return _context.Pecas.AnyAsync(p => p.Codigo.ToLower() == c && (ignorarId == null || p.Id != ignorarId));
    }

    public async Task AdicionarAsync(Peca peca) => await _context.Pecas.AddAsync(peca);

    public async Task<bool> SalvarAlteracoesAsync() => await _context.SaveChangesAsync() > 0;
}

public class CausaFalhaRepository : ICausaFalhaRepository
{
    private readonly AdSyncDbContext _context;
    public CausaFalhaRepository(AdSyncDbContext context) => _context = context;

    public Task<CausaFalha?> ObterPorIdAsync(Guid id) =>
        _context.CausasFalha.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<CausaFalha>> ListarAsync(bool incluirInativos)
    {
        var query = _context.CausasFalha.AsNoTracking().AsQueryable();

        if (!incluirInativos)
            query = query.Where(c => c.Ativo);

        return await query.OrderBy(c => c.Descricao).ToListAsync();
    }

    public Task<bool> DescricaoExisteAsync(string descricao, Guid? ignorarId = null)
    {
        var d = descricao.Trim().ToLower();
        return _context.CausasFalha.AnyAsync(c => c.Descricao.ToLower() == d && (ignorarId == null || c.Id != ignorarId));
    }

    public async Task AdicionarAsync(CausaFalha causa) => await _context.CausasFalha.AddAsync(causa);

    public async Task<bool> SalvarAlteracoesAsync() => await _context.SaveChangesAsync() > 0;
}

public class EquipamentoRepository : IEquipamentoRepository
{
    private readonly AdSyncDbContext _context;
    public EquipamentoRepository(AdSyncDbContext context) => _context = context;

    public Task<Equipamento?> ObterPorIdAsync(Guid id) =>
        _context.Equipamentos.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<Equipamento>> ListarAsync(bool incluirInativos, string? setor)
    {
        var query = _context.Equipamentos.AsNoTracking().AsQueryable();

        if (!incluirInativos)
            query = query.Where(e => e.Ativo);

        if (!string.IsNullOrWhiteSpace(setor))
        {
            var s = setor.Trim().ToLower();
            query = query.Where(e => e.Setor.ToLower() == s);
        }

        return await query.OrderBy(e => e.Setor).ThenBy(e => e.Nome).ToListAsync();
    }

    public Task<bool> CodigoPatrimonioExisteAsync(string codigo, Guid? ignorarId = null)
    {
        var c = codigo.Trim().ToLower();
        return _context.Equipamentos.AnyAsync(e => e.CodigoPatrimonio.ToLower() == c && (ignorarId == null || e.Id != ignorarId));
    }

    // Usado para impedir inativar um equipamento que ainda tem chamado rodando.
    public Task<bool> TemOrdensEmAbertoAsync(Guid equipamentoId) =>
        _context.OrdensServico.AnyAsync(os =>
            os.EquipamentoId == equipamentoId &&
            (os.Status == StatusOS.Aberta || os.Status == StatusOS.EmAndamento || os.Status == StatusOS.Pausada));

    public async Task AdicionarAsync(Equipamento equipamento) => await _context.Equipamentos.AddAsync(equipamento);

    public async Task<bool> SalvarAlteracoesAsync() => await _context.SaveChangesAsync() > 0;
}