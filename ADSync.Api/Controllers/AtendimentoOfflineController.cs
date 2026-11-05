using ADSync.Application.DTOs;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/ordensservico/{id:guid}/eventos")]
[Authorize(Roles = "Tecnico")]
public class AtendimentoOfflineController(AtendimentoOfflineService service, IConfiguration config) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Aplicar(Guid id, EventoAtendimentoDto evento)
    {
        if (!config.GetValue("Offline:Enabled", false))
            return StatusCode(503, new { mensagem = "Atendimento offline ainda não habilitado neste ambiente. Pendências preservadas." });
        if (id != evento.OrdemServicoId) throw new RegraNegocioException("Ordem do evento não corresponde ao endereço.");
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var user)) throw new AcessoNegadoException("Sessão inválida.");
        return Ok(await service.AplicarAsync(user, evento));
    }
}
