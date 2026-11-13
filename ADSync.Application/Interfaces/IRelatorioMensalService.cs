using ADSync.Application.DTOs;

namespace ADSync.Application.Interfaces;

public interface IRelatorioMensalService
{
    Task<RelatorioMensalDto> ObterAsync(int ano, int mes);
}
