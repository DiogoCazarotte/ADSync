using ADSync.Application.DTOs;
using ADSync.Domain.Enums;

namespace ADSync.Application.Interfaces;

public interface IOrdemServicoService
{
    Task<OrdemServicoDto> AbrirAsync(CriarOrdemServicoDto dto);
    Task<OrdemServicoDetalheDto?> ObterDetalheAsync(Guid id, Guid? tecnicoId = null, Guid? operadorId = null);
    Task<List<OrdemServicoDto>> ListarAsync(StatusOS? status, PrioridadeOS? prioridade, Guid? tecnicoId, Guid? operadorId);

    // Ações do fluxo
    Task<OrdemServicoDetalheDto> AtribuirTecnicoAsync(Guid id, Guid tecnicoId);
    Task<OrdemServicoDetalheDto> IniciarAsync(Guid id, Guid usuarioId, DateTimeOffset? ocorridoEm);
    Task<OrdemServicoDetalheDto> PausarAsync(Guid id, Guid usuarioId, string motivo, DateTimeOffset? ocorridoEm);
    Task<OrdemServicoDetalheDto> RetomarAsync(Guid id, Guid usuarioId, DateTimeOffset? ocorridoEm);
    Task<OrdemServicoDetalheDto> ConcluirAsync(Guid id, Guid usuarioId, Guid causaFalhaId, string? observacoes, DateTimeOffset? ocorridoEm);
    Task<OrdemServicoDetalheDto> CancelarAsync(Guid id, string? motivo);
    Task<OrdemServicoDetalheDto> RegistrarPecaAsync(Guid id, Guid usuarioId, Guid pecaId, int quantidade);
}
