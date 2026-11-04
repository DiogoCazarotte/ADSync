using ADSync.Application.DTOs;

namespace ADSync.Application.Interfaces;

public interface IIndicadoresService
{
    Task<IndicadoresDto> ObterAsync(DateOnly dataInicio, DateOnly dataFim, string? setor);
}