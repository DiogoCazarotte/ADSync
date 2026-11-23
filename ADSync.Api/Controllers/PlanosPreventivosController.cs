using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Services;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/planos-preventivos")]
[Authorize(Roles = "Gestor,Tecnico")]
public class PlanosPreventivosController : ControllerBase
{
    private readonly IPlanoPreventivoService _service;
    private readonly AtendimentoPreventivoService _atendimento;
    public PlanosPreventivosController(IPlanoPreventivoService service, AtendimentoPreventivoService atendimento)
        => (_service, _atendimento) = (service, atendimento);

    // GET /api/planos-preventivos?equipamentoId=... -> Gestor ou Técnico consulta
    [HttpGet]
    public async Task<ActionResult<List<PlanoPreventivoDto>>> Listar(
        [FromQuery] Guid? equipamentoId, [FromQuery] bool incluirInativos = false)
    {
        incluirInativos = incluirInativos && User.IsInRole("Gestor");
        var planos = await _service.ListarAsync(incluirInativos, equipamentoId);
        planos.ForEach(DefinirPermissaoAtendimento);
        return Ok(planos);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanoPreventivoDto>> Obter(Guid id)
    {
        var p = await _service.ObterAsync(id);
        if (p is not null) DefinirPermissaoAtendimento(p);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("{id:guid}/iniciar")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Iniciar(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var tecnicoId))
            throw new AcessoNegadoException("Token inválido.");
        return Ok(await _atendimento.IniciarAsync(id, tecnicoId, ct));
    }

    private void DefinirPermissaoAtendimento(PlanoPreventivoDto p)
        => p.PodeAtender = User.IsInRole("Tecnico") && Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
            && (p.OrdemServicoId.HasValue ? p.TecnicoOrdemServicoId : p.TecnicoResponsavelId) == id;

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
