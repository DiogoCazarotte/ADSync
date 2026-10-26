using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/causas-falha")]
[Authorize] // listar/consultar: qualquer usuário logado. Cadastrar/alterar: só Gestor.
public class CausasFalhaController : ControllerBase
{
    private readonly ICausaFalhaService _service;
    public CausasFalhaController(ICausaFalhaService service) => _service = service;

    // GET /api/causas-falha?incluirInativos=true
    [HttpGet]
    public async Task<ActionResult<List<CausaFalhaDto>>> Listar([FromQuery] bool incluirInativos = false)
    {
        var verInativos = incluirInativos && User.IsInRole("Gestor");
        return Ok(await _service.ListarAsync(verInativos));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CausaFalhaDto>> ObterPorId(Guid id)
    {
        var c = await _service.ObterAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<CausaFalhaDto>> Criar([FromBody] SalvarCausaFalhaDto dto)
    {
        var criada = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<CausaFalhaDto>> Atualizar(Guid id, [FromBody] SalvarCausaFalhaDto dto)
        => Ok(await _service.AtualizarAsync(id, dto));

    [HttpPost("{id:guid}/inativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<CausaFalhaDto>> Inativar(Guid id)
        => Ok(await _service.AlterarSituacaoAsync(id, ativo: false));

    [HttpPost("{id:guid}/ativar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<CausaFalhaDto>> Ativar(Guid id)
        => Ok(await _service.AlterarSituacaoAsync(id, ativo: true));
}