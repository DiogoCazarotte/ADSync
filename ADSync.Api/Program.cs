using ADSync.Application.Interfaces;
using ADSync.Application.Services;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using ADSync.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<AdSyncDbContext>(options => options.UseNpgsql(dataSource));

// ---------- Identity (login, senha, papeis de usuario) ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AdSyncDbContext>()
    .AddDefaultTokenProviders();

// ---------- Injecao de dependencia: Repository + Service ----------
builder.Services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
builder.Services.AddScoped<IOrdemServicoService, OrdemServicoService>();

// ---------- Controllers + OpenAPI ----------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();