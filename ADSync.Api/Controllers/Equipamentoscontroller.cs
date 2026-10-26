using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/equipamentos")]
[Authorize] // listar/consultar: qualquer usuário logado. Cadastrar/alterar: só Gestor.
public class EquipamentosController : ControllerBase
{
    private readonly IEquipamentoService _service;
    public EquipamentosController(IEquipamentoService service) => _service = service;

    // GET /api/equipamentos?setor=Costura&incluirInativos=true
    [HttpGet]
    public async Task<ActionResult<List<EquipamentoDto>>> Listar(
        [FromQuery] string? setor,
        [FromQuery] bool incluirInativos = false)
    {
        // Só o Gestor enxerga os inativos
        var verInativos = incluirInativos && User.IsInRole("Gestor");
        return Ok(await _service.ListarAsync(verInativos, setor));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipamentoDto>> ObterPorId(Guid id)
    {
        var e = await _service.ObterAsync(id);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpPost]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<EquipamentoDto>> Criar([FromBody] SalvarEquipamentoDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<EquipamentoDto>> Atualizar(Guid id, [FromBody] SalvarEquipamentoDto dto)
        => Ok(await _service.AtualizarAsync(id, dto));

    [HttpPost("{id:guid}/inativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<EquipamentoDto>> Inativar(Guid id)
        => Ok(await _service.AlterarSituacaoAsync(id, ativo: false));

    [HttpPost("{id:guid}/ativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<EquipamentoDto>> Ativar(Guid id)
        => Ok(await _service.AlterarSituacaoAsync(id, ativo: true));
}