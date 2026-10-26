using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/planos-preventivos")]
[Authorize]
public class PlanosPreventivosController : ControllerBase
{
    private readonly IPlanoPreventivoService _service;
    public PlanosPreventivosController(IPlanoPreventivoService service) => _service = service;

    // GET /api/planos-preventivos?equipamentoId=...  -> qualquer usuário logado consulta
    [HttpGet]
    public async Task<ActionResult<List<PlanoPreventivoDto>>> Listar(
        [FromQuery] Guid? equipamentoId, [FromQuery] bool incluirInativos = false)
    {
        incluirInativos = incluirInativos && User.IsInRole("Gestor");
        return Ok(await _service.ListarAsync(incluirInativos, equipamentoId));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanoPreventivoDto>> Obter(Guid id)
    {
        var p = await _service.ObterAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    // Só o Gestor planeja a preventiva: é ele que decide equipamento, tarefa e periodicidade.
    [HttpPost]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PlanoPreventivoDto>> Criar([FromBody] SalvarPlanoPreventivoDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PlanoPreventivoDto>> Atualizar(Guid id, [FromBody] SalvarPlanoPreventivoDto dto) =>
        Ok(await _service.AtualizarAsync(id, dto));

    [HttpPost("{id:guid}/inativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PlanoPreventivoDto>> Inativar(Guid id) =>
        Ok(await _service.AlterarSituacaoAsync(id, false));

    [HttpPost("{id:guid}/ativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PlanoPreventivoDto>> Ativar(Guid id) =>
        Ok(await _service.AlterarSituacaoAsync(id, true));
}