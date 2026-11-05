using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/pecas")]
[Authorize(Roles = "Gestor,Tecnico")] // Estoque restrito à manutenção; alterações cadastrais só pelo Gestor.
public class PecasController : ControllerBase
{
    private readonly IPecaService _service;
    public PecasController(IPecaService service) => _service = service;

    // GET /api/pecas?busca=rolamento
    [HttpGet]
    public async Task<ActionResult<List<PecaDto>>> Listar([FromQuery] string? busca)
        => Ok(await _service.ListarAsync(busca));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PecaDto>> ObterPorId(Guid id)
    {
        var p = await _service.ObterAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PecaDto>> Criar([FromBody] CriarPecaDto dto)
    {
        var criada = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    // Altera só código e nome (o estoque muda por entrada ou por uso em O.S.)
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PecaDto>> Atualizar(Guid id, [FromBody] AtualizarPecaDto dto)
        => Ok(await _service.AtualizarAsync(id, dto));

    // POST /api/pecas/{id}/entrada  -> compra/reposição de estoque
    [HttpPost("{id:guid}/entrada")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<PecaDto>> RegistrarEntrada(Guid id, [FromBody] EntradaEstoqueDto dto)
        => Ok(await _service.RegistrarEntradaAsync(id, dto.Quantidade));
}
