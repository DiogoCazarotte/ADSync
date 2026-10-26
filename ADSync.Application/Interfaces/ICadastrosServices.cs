using ADSync.Application.DTOs;
using ADSync.Domain.Enums;

namespace ADSync.Application.Interfaces;

public interface IEquipamentoService
{
    Task<List<EquipamentoDto>> ListarAsync(bool incluirInativos, string? setor);
    Task<EquipamentoDto?> ObterAsync(Guid id);
    Task<EquipamentoDto> CriarAsync(SalvarEquipamentoDto dto);
    Task<EquipamentoDto> AtualizarAsync(Guid id, SalvarEquipamentoDto dto);
    Task<EquipamentoDto> AlterarSituacaoAsync(Guid id, bool ativo);
}

public interface ICausaFalhaService
{
    Task<List<CausaFalhaDto>> ListarAsync(bool incluirInativos);
    Task<CausaFalhaDto?> ObterAsync(Guid id);
    Task<CausaFalhaDto> CriarAsync(SalvarCausaFalhaDto dto);
    Task<CausaFalhaDto> AtualizarAsync(Guid id, SalvarCausaFalhaDto dto);
    Task<CausaFalhaDto> AlterarSituacaoAsync(Guid id, bool ativo);
}

public interface IPecaService
{
    Task<List<PecaDto>> ListarAsync(string? busca);
    Task<PecaDto?> ObterAsync(Guid id);
    Task<PecaDto> CriarAsync(CriarPecaDto dto);
    Task<PecaDto> AtualizarAsync(Guid id, AtualizarPecaDto dto);
    Task<PecaDto> RegistrarEntradaAsync(Guid id, int quantidade);
}

public interface IUsuarioService
{
    Task<List<UsuarioDto>> ListarAsync(TipoUsuario? tipo, bool incluirInativos);
}
