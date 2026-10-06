using ADSync.Application.DTOs;
using ADSync.Domain.Enums;

namespace ADSync.Application.Interfaces;

public interface IOrdemServicoService
{
    Task<OrdemServicoDto> AbrirAsync(CriarOrdemServicoDto dto);
    Task<OrdemServicoDto?> ObterPorIdAsync(Guid id);
    Task<List<OrdemServicoDto>> ListarAsync(StatusOS? status, PrioridadeOS? prioridade);
}
