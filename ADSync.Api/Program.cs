using System.Text;
using ADSync.Api.Auth;
using ADSync.Api.Middleware;
using ADSync.Application.Interfaces;
using ADSync.Application.Services;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using ADSync.Infrastructure.Jobs;
using ADSync.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.NameTranslation;
using ADSync.Infrastructure.Safety;
using ADSync.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
// No Docker, o cliente publicado fica em /app/client e usa a mesma origem da API.
var serveClient = builder.Configuration.GetValue("Hosting:ServeClient", false);
var readOnly = builder.Configuration.GetValue("Safety:ReadOnly", true);
var seedEnabled = builder.Configuration.GetValue("Seed:Enabled", false);
var preventiveEnabled = builder.Configuration.GetValue("Preventivas:GeracaoAutomaticaHabilitada", false);
if (readOnly && (seedEnabled || preventiveEnabled))
    throw new InvalidOperationException("Modo somente leitura exige seed e geração preventiva desativados.");

// ---------- Banco de dados (PostgreSQL / Supabase) ----------
var connectionString = builder.Configuration.GetConnectionString("AdSync");

var enumTranslator = new NpgsqlNullNameTranslator();
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: enumTranslator);
dataSourceBuilder.MapEnum<StatusOS>("status_os_enum", nameTranslator: enumTranslator);
dataSourceBuilder.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: enumTranslator);
dataSourceBuilder.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: enumTranslator);
dataSourceBuilder.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: enumTranslator);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AdSyncDbContext>(options => options.UseNpgsql(dataSource, o =>
{
    o.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: enumTranslator);
    o.MapEnum<StatusOS>("status_os_enum", nameTranslator: enumTranslator);
    o.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: enumTranslator);
    o.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: enumTranslator);
    o.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: enumTranslator);
}).AddInterceptors(new ReadOnlySaveChangesInterceptor(readOnly)));

// ---------- Identity (usuários, senhas e papéis) ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AdSyncDbContext>()
    .AddDefaultTokenProviders();

// ---------- JWT (autenticação da API) ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Configuração 'Jwt:Key' ausente ou curta demais (mínimo 32 caracteres). " +
        "Defina com: dotnet user-secrets set \"Jwt:Key\" \"<chave longa e aleatória>\"");

builder.Services.Configure<JwtSettings>(jwtSection);
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ATENÇÃO: o AddIdentity acima define o cookie como esquema padrão, o que faria a API
// responder com redirecionamento (302) em vez de 401. Por isso reafirmamos o JWT como padrão aqui,
// DEPOIS do AddIdentity.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém os nomes curtos: "sub", "role", "name"...
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = AccountTokenValidation.ValidateAsync,
            OnChallenge = AccountTokenValidation.ChallengeAsync
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

// ---------- Injeção de dependência: Repository + Service ----------
builder.Services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IPecaRepository, PecaRepository>();
builder.Services.AddScoped<ICausaFalhaRepository, CausaFalhaRepository>();
builder.Services.AddScoped<IEquipamentoRepository, EquipamentoRepository>();
builder.Services.AddScoped<IOrdemServicoService, OrdemServicoService>();
builder.Services.AddScoped<ADSync.Infrastructure.Services.AtendimentoOfflineService>();
builder.Services.AddScoped<IEquipamentoService, EquipamentoService>();
builder.Services.AddScoped<ICausaFalhaService, CausaFalhaService>();
builder.Services.AddScoped<IPecaService, PecaService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

// ---------- Planos preventivos (RF13) ----------
builder.Services.AddScoped<IPlanoPreventivoRepository, PlanoPreventivoRepository>();
builder.Services.AddScoped<IPlanoPreventivoService, PlanoPreventivoService>();

// Job que abre as O.S. preventivas sozinho quando a data do plano chega.
if (preventiveEnabled)
    builder.Services.AddHostedService<GeracaoPreventivaBackgroundService>();

// ---------- Indicadores (RF07/RF10): MTTR, MTBF e % preventiva ----------
builder.Services.AddScoped<IIndicadoresRepository, IndicadoresRepository>();
builder.Services.AddScoped<IIndicadoresService, IndicadoresService>();

// ---------- CORS (o Blazor WASM roda em outra origem que a API) ----------
// Origens configuráveis em "Cors:Origens"; o padrão são as portas do launchSettings do Client.
var origensPermitidas = builder.Configuration.GetSection("Cors:Origens").Get<string[]>()
    ?? new[] { "http://localhost:5125", "https://localhost:7297" };
builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins(origensPermitidas)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ---------- Tradução de exceções de negócio em respostas HTTP (404 / 403 / 409) ----------
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// ---------- Controllers + OpenAPI ----------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Popula roles, usuários de exemplo, equipamentos e causas de falha (idempotente)
    if (seedEnabled)
    {
        using var scope = app.Services.CreateScope();
        await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration["Seed:SenhaDev"]);
    }

    // Console de testes (wwwroot/teste.html), só em desenvolvimento.
    // Mesma origem da API: não precisa de CORS. Abra https://localhost:<porta>/teste.html
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment() && app.Configuration.GetValue("Hosting:UseHttpsRedirection", true))
    app.UseHttpsRedirection();

if (serveClient) app.UsePublishedClient();

app.UseCors("Client");

app.UseAuthentication();
app.UseAuthorization();

// Login can invoke Identity SaveChanges (lockout/reset/rehash); the interceptor
// blocks those writes too. No business mutation or offline replay is accepted.
app.Use(async (context, next) =>
{
    if (readOnly && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method)
        && !(HttpMethods.IsPost(context.Request.Method) && context.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { mensagem = "Validação somente leitura: gravações e sincronização estão desativadas." });
        return;
    }
    await next();
});

app.MapGet("/api/runtime", () => new { readOnly, seedEnabled, preventiveEnabled,
    technicalOfflineEnabled = !readOnly && app.Configuration.GetValue("Offline:Enabled", false) });

app.MapControllers();

if (serveClient) app.MapPublishedClientFallback();

app.Run();
