using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    // =====================================================================
    //  Abrir / consultar / listar
    // =====================================================================

    // POST /api/ordensservico  -> só Operador e Gestor abrem chamado
    [HttpPost]
    [Authorize(Roles = "Operador,Gestor")]
    public async Task<ActionResult<OrdemServicoDto>> Abrir([FromBody] CriarOrdemServicoDto dto)
    {
        // O operador é SEMPRE quem está logado. Ignoramos qualquer OperadorId enviado no corpo,
        // senão um usuário poderia abrir O.S. em nome de outro.
        dto.OperadorId = UsuarioLogadoId;

        // Manutenção preventiva é planejada pelo Gestor; o Operador só reporta falhas (corretiva).
        if (!User.IsInRole("Gestor") &&
            string.Equals(dto.TipoManutencao, nameof(TipoManutencao.Preventiva), StringComparison.OrdinalIgnoreCase))
            throw new AcessoNegadoException("Apenas o Gestor pode abrir manutenção preventiva.");

        var criada = await _service.AbrirAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    // GET /api/ordensservico/{id}  -> qualquer usuário logado (detalhe completo)
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> ObterPorId(Guid id)
    {
        var os = await _service.ObterDetalheAsync(id);
        return os is null ? NotFound() : Ok(os);
    }

    // GET /api/ordensservico?status=Aberta&prioridade=Alta&minhas=true
    // minhas=true: Técnico vê as atribuídas a ele; Operador vê as que ele abriu; Gestor vê todas.
    [HttpGet]
    public async Task<ActionResult<List<OrdemServicoDto>>> Listar(
        [FromQuery] StatusOS? status,
        [FromQuery] PrioridadeOS? prioridade,
        [FromQuery] bool minhas = false)
    {
        Guid? tecnicoId = null;
        Guid? operadorId = null;

        if (minhas)
        {
            if (User.IsInRole("Tecnico")) tecnicoId = UsuarioLogadoId;
            else if (User.IsInRole("Operador")) operadorId = UsuarioLogadoId;
        }

        var lista = await _service.ListarAsync(status, prioridade, tecnicoId, operadorId);
        return Ok(lista);
    }

    // =====================================================================
    //  Ações do Gestor
    // =====================================================================

    // POST /api/ordensservico/{id}/atribuir
    [HttpPost("{id:guid}/atribuir")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Atribuir(Guid id, [FromBody] AtribuirTecnicoDto dto)
    {
        return Ok(await _service.AtribuirTecnicoAsync(id, dto.TecnicoId!.Value));
    }

    // POST /api/ordensservico/{id}/cancelar  (corpo opcional)
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "Gestor")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Cancelar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelarOrdemServicoDto? dto)
    {
        return Ok(await _service.CancelarAsync(id, dto?.Motivo));
    }

    // =====================================================================
    //  Ações do Técnico (só o técnico responsável pela O.S. consegue executar)
    // =====================================================================

    // POST /api/ordensservico/{id}/iniciar  (corpo opcional)
    [HttpPost("{id:guid}/iniciar")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Iniciar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EventoTempoDto? dto)
    {
        return Ok(await _service.IniciarAsync(id, UsuarioLogadoId, dto?.OcorridoEm));
    }

    // POST /api/ordensservico/{id}/pausar
    [HttpPost("{id:guid}/pausar")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Pausar(Guid id, [FromBody] PausarOrdemServicoDto dto)
    {
        return Ok(await _service.PausarAsync(id, UsuarioLogadoId, dto.Motivo, dto.OcorridoEm));
    }

    // POST /api/ordensservico/{id}/retomar  (corpo opcional)
    [HttpPost("{id:guid}/retomar")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Retomar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] EventoTempoDto? dto)
    {
        return Ok(await _service.RetomarAsync(id, UsuarioLogadoId, dto?.OcorridoEm));
    }

    // POST /api/ordensservico/{id}/pecas
    [HttpPost("{id:guid}/pecas")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> RegistrarPeca(Guid id, [FromBody] RegistrarPecaDto dto)
    {
        return Ok(await _service.RegistrarPecaAsync(id, UsuarioLogadoId, dto.PecaId!.Value, dto.Quantidade));
    }

    // POST /api/ordensservico/{id}/concluir
    [HttpPost("{id:guid}/concluir")]
    [Authorize(Roles = "Tecnico")]
    public async Task<ActionResult<OrdemServicoDetalheDto>> Concluir(Guid id, [FromBody] ConcluirOrdemServicoDto dto)
    {
        return Ok(await _service.ConcluirAsync(
            id, UsuarioLogadoId, dto.CausaFalhaId!.Value, dto.ObservacoesTecnicas, dto.OcorridoEm));
    }

    // Lê o Id do usuário a partir do claim "sub" do token
    private Guid UsuarioLogadoId =>
        Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new AcessoNegadoException("Token inválido.");
}