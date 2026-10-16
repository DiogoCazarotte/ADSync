using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // tudo aqui exige token válido; cada ação ainda pode restringir por papel
public class OrdensServicoController : ControllerBase
{
    private readonly IOrdemServicoService _service;

    public OrdensServicoController(IOrdemServicoService service)
    {
        _service = service;
    }

    // POST /api/ordensservico  -> só Operador e Gestor abrem chamado
    [HttpPost]
    [Authorize(Roles = "Operador,Gestor")]
    public async Task<ActionResult<OrdemServicoDto>> Abrir([FromBody] CriarOrdemServicoDto dto)
    {
        // O operador é SEMPRE quem está logado. Ignoramos qualquer OperadorId enviado no corpo,
        // senão um usuário poderia abrir O.S. em nome de outro.
        var usuarioId = ObterUsuarioId();
        if (usuarioId is null)
            return Unauthorized();

        dto.OperadorId = usuarioId.Value;

        var criada = await _service.AbrirAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    // GET /api/ordensservico/{id}  -> qualquer usuário logado
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrdemServicoDto>> ObterPorId(Guid id)
    {
        var os = await _service.ObterPorIdAsync(id);
        return os is null ? NotFound() : Ok(os);
    }

    // GET /api/ordensservico?status=Aberta&prioridade=Alta  -> qualquer usuário logado
    [HttpGet]
    public async Task<ActionResult<List<OrdemServicoDto>>> Listar(
        [FromQuery] StatusOS? status,
        [FromQuery] PrioridadeOS? prioridade)
    {
        var lista = await _service.ListarAsync(status, prioridade);
        return Ok(lista);
    }

    // Lê o Id do usuário a partir do claim "sub" do token
    private Guid? ObterUsuarioId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}