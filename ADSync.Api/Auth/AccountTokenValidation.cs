using ADSync.Api.Middleware;
using ADSync.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace ADSync.Api.Auth;

public static class AccountTokenValidation
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        if (context.HttpContext.RequestAborted.IsCancellationRequested) { context.Fail("Requisição cancelada."); return; }
        var rawId = context.Principal?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(rawId, out var id)) { context.Fail("Sessão inválida."); return; }
        try
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AdSyncDbContext>();
            var perfil = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, context.HttpContext.RequestAborted);
            var roles = context.Principal!.FindAll("role").Select(c => c.Value).ToArray();
            if (perfil is null || !perfil.Ativo || roles.Length != 1 || roles[0] != perfil.TipoUsuario.ToString())
                context.Fail("Conta ou perfil alterado. Entre novamente.");
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.Fail("Requisição cancelada.");
        }
        catch (Exception ex) when (DatabaseAvailability.IsUnavailable(ex))
        {
            // Sem banco não se presume uma conta válida nem se acusa senha/perfil inválido.
            context.HttpContext.Items[DatabaseAvailability.UnavailableKey] = true;
            context.Fail("Não foi possível verificar a conta enquanto o banco está indisponível.");
        }
    }
    public static async Task ChallengeAsync(JwtBearerChallengeContext context)
    {
        if (context.HttpContext.RequestAborted.IsCancellationRequested) { context.HandleResponse(); return; }
        if (context.HttpContext.Items.ContainsKey(DatabaseAvailability.UnavailableKey))
        {
            context.HandleResponse();
            await DatabaseAvailability.WriteResponseAsync(context.HttpContext, context.HttpContext.RequestAborted);
        }
    }
}
