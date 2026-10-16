using ADSync.Api.Auth;
using ADSync.Application.DTOs;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ADSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AdSyncDbContext _context;
    private readonly IJwtTokenService _tokenService;

    // Aqui o controller usa o Identity (UserManager/SignInManager) direto: eles já são
    // a "camada de serviço" da autenticação, então não criamos outra por cima.
    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AdSyncDbContext context,
        IJwtTokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _tokenService = tokenService;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto dto)
    {
        // Mesma mensagem para "e-mail não existe" e "senha errada",
        // para não revelar a ninguém quais e-mails estão cadastrados.
        const string credenciaisInvalidas = "E-mail ou senha inválidos.";

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return Unauthorized(new { mensagem = credenciaisInvalidas });

        // lockoutOnFailure: depois de várias senhas erradas seguidas, a conta é bloqueada por um tempo
        var resultado = await _signInManager.CheckPasswordSignInAsync(user, dto.Senha, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
            return Unauthorized(new { mensagem = "Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde." });

        if (!resultado.Succeeded)
            return Unauthorized(new { mensagem = credenciaisInvalidas });

        // Dados de negócio (nome, ativo) ficam na tabela "usuarios"
        var perfil = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);
        if (perfil is null || !perfil.Ativo)
            return Unauthorized(new { mensagem = "Usuário inativo. Procure o gestor." });

        var papeis = await _userManager.GetRolesAsync(user);
        var (token, expiraEm) = _tokenService.GerarToken(user.Id, perfil.Email, perfil.Nome, papeis);

        return Ok(new LoginResponseDto
        {
            Token = token,
            ExpiraEm = expiraEm,
            Nome = perfil.Nome,
            Email = perfil.Email,
            Papeis = papeis.ToList()
        });
    }

    // GET /api/auth/eu  -> mostra quem o token diz que você é (útil para testar)
    [HttpGet("eu")]
    [Authorize]
    public ActionResult Eu()
    {
        return Ok(new
        {
            id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            nome = User.FindFirst("name")?.Value,
            email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            papeis = User.FindAll("role").Select(c => c.Value)
        });
    }
}