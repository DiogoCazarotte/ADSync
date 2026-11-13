using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize(Roles = "Gestor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RelatoriosController(IRelatorioMensalService service) : ControllerBase
{
    [HttpGet("mensal")]
    public async Task<ActionResult<RelatorioMensalDto>> Mensal([FromQuery] int ano, [FromQuery] int mes)
    {
        try { return Ok(await service.ObterAsync(ano, mes)); }
        catch (ArgumentException ex) { return BadRequest(new { mensagem = ex.Message }); }
    }
}
