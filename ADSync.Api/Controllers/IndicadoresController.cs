using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

// RF07/RF10: painel de indicadores. É dado de gestão, então só o Gestor acessa.
[ApiController]
[Route("api/indicadores")]
[Authorize(Roles = "Gestor")]
public class IndicadoresController : ControllerBase
{
    private readonly IIndicadoresService _service;
    public IndicadoresController(IIndicadoresService service) => _service = service;

    // GET /api/indicadores?dataInicio=2026-10-01&dataFim=2026-10-31&setor=Costura
    // Datas não informadas -> últimos 30 dias (inclui hoje). "setor" é opcional.
    [HttpGet]
    public async Task<ActionResult<IndicadoresDto>> Obter(
        [FromQuery] DateOnly? dataInicio, [FromQuery] DateOnly? dataFim, [FromQuery] string? setor)
    {
        var fim = dataFim ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = dataInicio ?? DateOnly.FromDayNumber(Math.Max(0, fim.DayNumber - 29));

        if (inicio > fim)
            return BadRequest(new { mensagem = "A data inicial deve ser menor ou igual à data final." });

        if (fim == DateOnly.MaxValue)
            return BadRequest(new { mensagem = "A data final deve ser até 30/12/9999." });

        return Ok(await _service.ObterAsync(inicio, fim, setor));
    }
}
