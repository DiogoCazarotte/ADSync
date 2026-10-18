using ADSync.Domain.Entities;

namespace ADSync.Domain.Interfaces;

// Repositórios simples, usados para buscar registros de apoio ao fluxo da O.S.
// (mais tarde podem ganhar listagem, cadastro etc.)

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id);
}

public interface IPecaRepository
{
    Task<Peca?> ObterPorIdAsync(Guid id);
}

public interface ICausaFalhaRepository
{
    Task<CausaFalha?> ObterPorIdAsync(Guid id);
}

public interface IEquipamentoRepository
{
    Task<Equipamento?> ObterPorIdAsync(Guid id);
}