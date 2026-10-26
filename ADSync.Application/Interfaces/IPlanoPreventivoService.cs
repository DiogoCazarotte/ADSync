using ADSync.Application.DTOs;

namespace ADSync.Application.Interfaces;

public interface IPlanoPreventivoService
{
    Task<List<PlanoPreventivoDto>> ListarAsync(bool incluirInativos, Guid? equipamentoId);
    Task<PlanoPreventivoDto?> ObterAsync(Guid id);
    Task<PlanoPreventivoDto> CriarAsync(SalvarPlanoPreventivoDto dto);
    Task<PlanoPreventivoDto> AtualizarAsync(Guid id, SalvarPlanoPreventivoDto dto);
    Task<PlanoPreventivoDto> AlterarSituacaoAsync(Guid id, bool ativo);
}