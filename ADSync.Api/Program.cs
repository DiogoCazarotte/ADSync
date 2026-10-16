using System.Text;
using ADSync.Api.Auth;
using ADSync.Application.Interfaces;
using ADSync.Application.Services;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using ADSync.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Npgsql.NameTranslation;

var builder = WebApplication.CreateBuilder(args);

// ---------- Banco de dados (PostgreSQL / Supabase) ----------
var connectionString = builder.Configuration.GetConnectionString("AdSync");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: new NpgsqlNullNameTranslator());
dataSourceBuilder.MapEnum<StatusOS>("status_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
dataSourceBuilder.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
dataSourceBuilder.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: new NpgsqlNullNameTranslator());
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AdSyncDbContext>(options => options.UseNpgsql(dataSource, o =>
{
    o.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: new NpgsqlNullNameTranslator());
    o.MapEnum<StatusOS>("status_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
    o.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: new NpgsqlNullNameTranslator());
    o.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: new NpgsqlNullNameTranslator());
}));

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
builder.Services.AddScoped<IOrdemServicoService, OrdemServicoService>();

// ---------- Controllers + OpenAPI ----------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Popula roles, usuários de exemplo, equipamentos e causas de falha (idempotente)
    using var scope = app.Services.CreateScope();
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration["Seed:SenhaDev"]);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();