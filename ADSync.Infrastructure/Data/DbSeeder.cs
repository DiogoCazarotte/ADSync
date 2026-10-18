using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADSync.Infrastructure.Data;

// Popula o banco com dados iniciais para desenvolvimento.
// É idempotente: cada etapa confere se o dado já existe antes de criar,
// então pode rodar a cada inicialização sem duplicar nada.
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string? senhaDev)
    {
        var context = services.GetRequiredService<AdSyncDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await SeedRolesAsync(roleManager, logger);
        await SeedUsuariosAsync(context, userManager, senhaDev, logger);
        await SeedEquipamentosAsync(context, logger);
        await SeedCausasFalhaAsync(context, logger);
        await SeedPecasAsync(context, logger);
    }

    // ---------- Roles do Identity (uma para cada TipoUsuario) ----------
    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, ILogger logger)
    {
        foreach (var nomeRole in Enum.GetNames<TipoUsuario>())
        {
            if (await roleManager.RoleExistsAsync(nomeRole))
                continue;

            var resultado = await roleManager.CreateAsync(new IdentityRole<Guid>(nomeRole));
            if (resultado.Succeeded)
                logger.LogInformation("Role criada: {Role}", nomeRole);
            else
                logger.LogError("Erro ao criar role {Role}: {Erros}", nomeRole,
                    string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }
    }

    // ---------- Usuários: ApplicationUser (login) + Usuario (perfil), mesmo Id ----------
    private static async Task SeedUsuariosAsync(
        AdSyncDbContext context,
        UserManager<ApplicationUser> userManager,
        string? senhaDev,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(senhaDev))
        {
            logger.LogWarning(
                "Seed de usuários ignorado: a configuração 'Seed:SenhaDev' não foi definida. " +
                "Defina com: dotnet user-secrets set \"Seed:SenhaDev\" \"<senha>\"");
            return;
        }

        var usuarios = new[]
        {
            (Nome: "Paulo Henrique Souza Martins", Email: "paulo.gestor@sustennutri.com.br", Tipo: TipoUsuario.Gestor),
            (Nome: "Carlos Eduardo Batista Ferreira", Email: "carlos.tecnico@sustennutri.com.br", Tipo: TipoUsuario.Tecnico),
            (Nome: "Renata Oliveira Campos Dias", Email: "renata.operadora@sustennutri.com.br", Tipo: TipoUsuario.Operador),
        };

        foreach (var (nome, email, tipo) in usuarios)
        {
            // 1) Login (Identity)
            var appUser = await userManager.FindByEmailAsync(email);
            if (appUser is null)
            {
                appUser = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var resultado = await userManager.CreateAsync(appUser, senhaDev);
                if (!resultado.Succeeded)
                {
                    logger.LogError("Erro ao criar usuário {Email}: {Erros}", email,
                        string.Join("; ", resultado.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            // 2) Papel (role)
            if (!await userManager.IsInRoleAsync(appUser, tipo.ToString()))
                await userManager.AddToRoleAsync(appUser, tipo.ToString());

            // 3) Perfil na tabela "usuarios", com o MESMO Id do login
            var perfilExiste = await context.Usuarios.AnyAsync(u => u.Id == appUser.Id);
            if (!perfilExiste)
            {
                context.Usuarios.Add(new Usuario
                {
                    Id = appUser.Id,
                    Nome = nome,
                    Email = email,
                    TipoUsuario = tipo,
                    Ativo = true,
                    DataCriacao = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Usuário criado: {Email} ({Tipo})", email, tipo);
            }
        }
    }

    // ---------- Equipamentos (baseados na descrição da demanda da SustenNutri) ----------
    private static async Task SeedEquipamentosAsync(AdSyncDbContext context, ILogger logger)
    {
        if (await context.Equipamentos.AnyAsync())
            return;

        var lista = new List<Equipamento>();

        for (var i = 1; i <= 9; i++)
            lista.Add(new Equipamento { Nome = $"Máquina de Costura {i:00}", CodigoPatrimonio = $"COS-{i:000}", Setor = "Costura" });

        lista.Add(new Equipamento { Nome = "Ensacadeira Linha 1", CodigoPatrimonio = "ENS-001", Setor = "Ensaque" });
        lista.Add(new Equipamento { Nome = "Ensacadeira Linha 2", CodigoPatrimonio = "ENS-002", Setor = "Ensaque" });
        lista.Add(new Equipamento { Nome = "Triturador com Peneiras", CodigoPatrimonio = "TRI-001", Setor = "Moagem" });
        lista.Add(new Equipamento { Nome = "Motor Elétrico - Moagem", CodigoPatrimonio = "MOT-001", Setor = "Moagem" });
        lista.Add(new Equipamento { Nome = "Motor Elétrico - Ensaque", CodigoPatrimonio = "MOT-002", Setor = "Ensaque" });
        lista.Add(new Equipamento { Nome = "Redutor - Moagem", CodigoPatrimonio = "RED-001", Setor = "Moagem" });
        lista.Add(new Equipamento { Nome = "Redutor - Ensaque", CodigoPatrimonio = "RED-002", Setor = "Ensaque" });

        context.Equipamentos.AddRange(lista);
        await context.SaveChangesAsync();
        logger.LogInformation("{Qtd} equipamentos criados.", lista.Count);
    }

    // ---------- Causas de falha (lista pré-cadastrada, RF05) ----------
    private static async Task SeedCausasFalhaAsync(AdSyncDbContext context, ILogger logger)
    {
        if (await context.CausasFalha.AnyAsync())
            return;

        var descricoes = new[]
        {
            "Desgaste de rolamento",
            "Sobrecarga elétrica",
            "Falta de lubrificação",
            "Quebra de agulha",
            "Correia rompida ou desgastada",
            "Sensor com defeito",
            "Peneira entupida ou rasgada",
            "Falha na engrenagem do redutor",
            "Superaquecimento do motor",
            "Erro de operação",
        };

        context.CausasFalha.AddRange(descricoes.Select(d => new CausaFalha { Descricao = d }));
        await context.SaveChangesAsync();
        logger.LogInformation("{Qtd} causas de falha criadas.", descricoes.Length);
    }

    // ---------- Peças de reposição (exemplos para teste do fluxo, RF04) ----------
    private static async Task SeedPecasAsync(AdSyncDbContext context, ILogger logger)
    {
        if (await context.Pecas.AnyAsync())
            return;

        var pecas = new[]
        {
            (Codigo: "ROL-6204", Nome: "Rolamento 6204 ZZ", Estoque: 12),
            (Codigo: "ROL-6205", Nome: "Rolamento 6205 ZZ", Estoque: 8),
            (Codigo: "COR-A45", Nome: "Correia em V A45", Estoque: 6),
            (Codigo: "AGU-DPX5", Nome: "Agulha industrial DPx5 nº 16", Estoque: 100),
            (Codigo: "SEN-IND-M12", Nome: "Sensor indutivo M12", Estoque: 5),
            (Codigo: "PEN-3MM", Nome: "Peneira de aço 3 mm", Estoque: 4),
            (Codigo: "RET-35527", Nome: "Retentor 35x52x7", Estoque: 10),
            (Codigo: "FUS-10A", Nome: "Fusível 10 A", Estoque: 30),
            (Codigo: "OLE-ISO220", Nome: "Óleo para redutor ISO VG 220 (1 L)", Estoque: 20),
            (Codigo: "GRA-EP2", Nome: "Graxa EP2 (cartucho)", Estoque: 15),
        };

        context.Pecas.AddRange(pecas.Select(p => new Peca
        {
            Codigo = p.Codigo,
            Nome = p.Nome,
            QuantidadeEstoque = p.Estoque
        }));

        await context.SaveChangesAsync();
        logger.LogInformation("{Qtd} peças criadas.", pecas.Length);
    }
}