using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Gestor")] // dados de pessoas: só o Gestor consulta
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _service;
    public UsuariosController(IUsuarioService service) => _service = service;

    // GET /api/usuarios?tipo=Tecnico&incluirInativos=true
    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar(
        [FromQuery] TipoUsuario? tipo,
        [FromQuery] bool incluirInativos = false)
        => Ok(await _service.ListarAsync(tipo, incluirInativos));
}