using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADSync.Domain.Entities;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Gestor")] // dados de pessoas: só o Gestor consulta
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _service;
    private readonly AdSyncDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public UsuariosController(IUsuarioService service, AdSyncDbContext db, UserManager<ApplicationUser> users)
        => (_service, _db, _users) = (service, db, users);

    // GET /api/usuarios?tipo=Tecnico&incluirInativos=true
    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> Listar(
        [FromQuery] TipoUsuario? tipo,
        [FromQuery] bool incluirInativos = false)
        => Ok(await _service.ListarAsync(tipo, incluirInativos));

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar(CriarUsuarioDto dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var conta = new ApplicationUser { Id = Guid.NewGuid(), UserName = dto.Email.Trim(), Email = dto.Email.Trim() };
        Check(await _users.CreateAsync(conta, dto.Senha));
        Check(await _users.AddToRoleAsync(conta, dto.TipoUsuario.ToString()));
        var perfil = new Usuario { Id = conta.Id, Nome = dto.Nome.Trim(), Email = conta.Email!, TipoUsuario = dto.TipoUsuario };
        _db.Usuarios.Add(perfil);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(Map(perfil));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UsuarioDto>> Editar(Guid id, SalvarUsuarioDto dto)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var perfil = await Find(id);
        if (User.FindFirst("sub")?.Value == id.ToString() && dto.TipoUsuario != TipoUsuario.Gestor)
            throw new RegraNegocioException("Você não pode remover o próprio perfil de gestor.");
        var conta = await _users.FindByIdAsync(id.ToString()) ?? throw new NaoEncontradoException("Conta não encontrada.");
        conta.Email = dto.Email.Trim(); conta.UserName = conta.Email;
        Check(await _users.UpdateAsync(conta));
        var roles = await _users.GetRolesAsync(conta);
        Check(await _users.RemoveFromRolesAsync(conta, roles));
        Check(await _users.AddToRoleAsync(conta, dto.TipoUsuario.ToString()));
        perfil.Nome = dto.Nome.Trim(); perfil.Email = conta.Email!; perfil.TipoUsuario = dto.TipoUsuario;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(Map(perfil));
    }

    [HttpPut("{id:guid}/situacao")]
    public async Task<ActionResult<UsuarioDto>> Situacao(Guid id, SituacaoUsuarioDto dto)
    {
        if (!dto.Ativo && User.FindFirst("sub")?.Value == id.ToString())
            throw new RegraNegocioException("Você não pode desativar a própria conta.");
        var perfil = await Find(id);
        perfil.Ativo = dto.Ativo;
        await _db.SaveChangesAsync();
        return Ok(Map(perfil));
    }

    private async Task<Usuario> Find(Guid id) => await _db.Usuarios.FindAsync(id)
        ?? throw new NaoEncontradoException("Usuário não encontrado.");
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new RegraNegocioException(string.Join(" ", result.Errors.Select(e => e.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "Este e-mail já está cadastrado.",
            "PasswordTooShort" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresDigit" or "PasswordRequiresLower" or "PasswordRequiresUpper" => "A senha precisa de pelo menos 8 caracteres, com maiúscula, minúscula, número e símbolo.",
            _ => "Não foi possível salvar a conta. Verifique os dados e tente novamente."
        }).Distinct()));
    }
    private static UsuarioDto Map(Usuario u) => new() { Id = u.Id, Nome = u.Nome, Email = u.Email, TipoUsuario = u.TipoUsuario.ToString(), Ativo = u.Ativo };
}
