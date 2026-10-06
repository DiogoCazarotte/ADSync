using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdensServicoController : ControllerBase
{
    private readonly IOrdemServicoService _service;

    public OrdensServicoController(IOrdemServicoService service)
    {
        _service = service;
    }

    // POST /api/ordensservico
    [HttpPost]
    public async Task<ActionResult<OrdemServicoDto>> Abrir([FromBody] CriarOrdemServicoDto dto)
    {
        var criada = await _service.AbrirAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    // GET /api/ordensservico/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrdemServicoDto>> ObterPorId(Guid id)
    {
        var os = await _service.ObterPorIdAsync(id);
        return os is null ? NotFound() : Ok(os);
    }

    // GET /api/ordensservico?status=Aberta&prioridade=Alta
    [HttpGet]
    public async Task<ActionResult<List<OrdemServicoDto>>> Listar(
        [FromQuery] StatusOS? status,
        [FromQuery] PrioridadeOS? prioridade)
    {
        var lista = await _service.ListarAsync(status, prioridade);
        return Ok(lista);
    }
}
