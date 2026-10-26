using ADSync.Domain.Entities;
using ADSync.Domain.Enums;

namespace ADSync.Domain.Interfaces;

// Repositórios dos cadastros de apoio (usados no fluxo da O.S. e nas telas de cadastro).

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id);
    Task<List<Usuario>> ListarAsync(TipoUsuario? tipo, bool incluirInativos);
}

public interface IPecaRepository
{
    Task<Peca?> ObterPorIdAsync(Guid id);
    Task<List<Peca>> ListarAsync(string? busca);
    Task<bool> CodigoExisteAsync(string codigo, Guid? ignorarId = null);
    Task AdicionarAsync(Peca peca);
    Task<bool> SalvarAlteracoesAsync();
}

public interface ICausaFalhaRepository
{
    Task<CausaFalha?> ObterPorIdAsync(Guid id);
    Task<List<CausaFalha>> ListarAsync(bool incluirInativos);
    Task<bool> DescricaoExisteAsync(string descricao, Guid? ignorarId = null);
    Task AdicionarAsync(CausaFalha causa);
    Task<bool> SalvarAlteracoesAsync();
}

public interface IEquipamentoRepository
{
    Task<Equipamento?> ObterPorIdAsync(Guid id);
    Task<List<Equipamento>> ListarAsync(bool incluirInativos, string? setor);
    Task<bool> CodigoPatrimonioExisteAsync(string codigo, Guid? ignorarId = null);
    Task<bool> TemOrdensEmAbertoAsync(Guid equipamentoId);
    Task AdicionarAsync(Equipamento equipamento);
    Task<bool> SalvarAlteracoesAsync();
}