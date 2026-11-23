using System.ComponentModel.DataAnnotations;
using ADSync.Api.Controllers;
using ADSync.Application.DTOs;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
bool Valid(object model) => Validator.TryValidateObject(model, new ValidationContext(model), [], true);
Check(!Valid(new AlterarSenhaUsuarioDto()), "Senha vazia aceita.");
Check(!Valid(new AlterarSenhaUsuarioDto { NovaSenha = "1234567", ConfirmarSenha = "1234567" }), "Senha curta aceita.");
Check(!Valid(new AlterarSenhaUsuarioDto { NovaSenha = new string('a', 129), ConfirmarSenha = new string('a', 129) }), "Senha longa aceita.");
Check(!Valid(new AlterarSenhaUsuarioDto { NovaSenha = "SenhaNova!123", ConfirmarSenha = "Diferente!123" }), "Confirmação divergente aceita.");
Check(Valid(new AlterarSenhaUsuarioDto { NovaSenha = "SenhaNova!123", ConfirmarSenha = "SenhaNova!123" }), "Senha válida recusada.");
Check(!Valid(new EditarDescricaoOrdemServicoDto { DescricaoProblema = "   " }), "Descrição em branco aceita.");
Check(!Valid(new EditarDescricaoOrdemServicoDto { DescricaoProblema = new string('a', 2001) }), "Descrição longa aceita.");
Console.WriteLine("PASS: validação exige senha confirmada de 8 a 128 caracteres e descrição preenchida de até 2000 caracteres.");

// Perfil rastreado e store do Identity em memória: nenhum banco é conectado ou alterado.
await using var db = new AdSyncDbContext(new DbContextOptionsBuilder<AdSyncDbContext>()
    .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1").Options);
var account = new ApplicationUser { Id = Guid.NewGuid(), UserName = "funcionario@example.test", SecurityStamp = Guid.NewGuid().ToString() };
var profile = new Usuario { Id = account.Id, Nome = "Funcionário", Email = account.UserName, TipoUsuario = TipoUsuario.Operador };
db.Usuarios.Attach(profile);
var store = new MemoryUsers(db, account);
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddSingleton<IUserStore<ApplicationUser>>(store);
services.AddIdentityCore<ApplicationUser>(o => o.Password.RequiredLength = 8).AddDefaultTokenProviders();
using var provider = services.BuildServiceProvider();
var manager = provider.GetRequiredService<UserManager<ApplicationUser>>();
account.PasswordHash = manager.PasswordHasher.HashPassword(account, "SenhaAntiga!123");
var oldHash = account.PasswordHash;
var oldStamp = account.SecurityStamp;
var controller = new UsuariosController(null!, db, manager);
try
{
    await controller.AlterarSenha(account.Id, new() { NovaSenha = "abcdefgh", ConfirmarSenha = "abcdefgh" });
    throw new Exception("Senha sem complexidade aceita.");
}
catch (RegraNegocioException ex) { Check(ex.Message.Contains("maiúscula"), "Validação de complexidade não traduzida."); }
Check(account.PasswordHash == oldHash && store.Saves == 0, "Senha inválida alterou a conta.");
var result = await controller.AlterarSenha(account.Id, new() { NovaSenha = "SenhaNova!123", ConfirmarSenha = "SenhaNova!123" });
Check(((UsuarioDto)((OkObjectResult)result.Result!).Value!).Id == profile.Id, "Resposta não identifica o funcionário.");
Check(store.Saves == 1 && account.PasswordHash != oldHash && account.SecurityStamp != oldStamp, "Reset não persistiu hash e security stamp.");
Check(await manager.CheckPasswordAsync(account, "SenhaNova!123"), "Nova senha não autentica.");
Check(!await manager.CheckPasswordAsync(account, "SenhaAntiga!123"), "Senha antiga continua autenticando.");
Check(profile.Nome == "Funcionário" && profile.TipoUsuario == TipoUsuario.Operador && profile.Ativo, "Troca de senha alterou o perfil.");
Console.WriteLine("PASS: troca via Identity rejeita senha fraca; nova senha autentica, antiga é recusada e perfil permanece intacto.");

sealed class MemoryUsers(AdSyncDbContext db, ApplicationUser account) : UserStore<ApplicationUser, IdentityRole<Guid>, AdSyncDbContext, Guid>(db)
{
    public int Saves { get; private set; }
    public override Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(userId == account.Id.ToString() ? account : null);
    public override Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        => Task.FromResult(normalizedUserName == account.UserName!.ToUpperInvariant() ? account : null);
    public override Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.FromResult(IdentityResult.Success);
    }
}
