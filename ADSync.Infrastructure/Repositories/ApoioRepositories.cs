using ADSync.Domain.Entities;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Infrastructure.Repositories;

// Implementações simples. Os registros voltam RASTREADOS pelo contexto de propósito:
// quando o fluxo altera a entidade (ex.: baixa de estoque da Peca), o mesmo
// SaveChanges da O.S. grava tudo junto.

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AdSyncDbContext _context;
    public UsuarioRepository(AdSyncDbContext context) => _context = context;

    public Task<Usuario?> ObterPorIdAsync(Guid id) =>
        _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
}

public class PecaRepository : IPecaRepository
{
    private readonly AdSyncDbContext _context;
    public PecaRepository(AdSyncDbContext context) => _context = context;

    public Task<Peca?> ObterPorIdAsync(Guid id) =>
        _context.Pecas.FirstOrDefaultAsync(p => p.Id == id);
}

public class CausaFalhaRepository : ICausaFalhaRepository
{
    private readonly AdSyncDbContext _context;
    public CausaFalhaRepository(AdSyncDbContext context) => _context = context;

    public Task<CausaFalha?> ObterPorIdAsync(Guid id) =>
        _context.CausasFalha.FirstOrDefaultAsync(c => c.Id == id);
}

public class EquipamentoRepository : IEquipamentoRepository
{
    private readonly AdSyncDbContext _context;
    public EquipamentoRepository(AdSyncDbContext context) => _context = context;

    public Task<Equipamento?> ObterPorIdAsync(Guid id) =>
        _context.Equipamentos.FirstOrDefaultAsync(e => e.Id == id);
}