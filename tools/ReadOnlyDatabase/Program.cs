using ADSync.Domain.Enums;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.NameTranslation;

// Reads only the existing API User Secrets. No environment connection overrides,
// schema operations, seed, SQL scripts, SaveChanges, or credential output.
try
{
    var config = new ConfigurationBuilder().AddUserSecrets("c6a39c55-2a48-4e31-bf5a-9996b0d92007").Build();
    var connection = config.GetConnectionString("AdSync")
        ?? throw new InvalidOperationException("Conexão ausente nos User Secrets.");
    var parsed = new NpgsqlConnectionStringBuilder(connection);
    if (string.IsNullOrWhiteSpace(parsed.Host)) throw new InvalidOperationException("Host ausente.");
    Console.WriteLine("Conexão dos User Secrets carregada; valores omitidos.");
    var translator = new NpgsqlNullNameTranslator();
    var sourceBuilder = new NpgsqlDataSourceBuilder(connection);
    sourceBuilder.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: translator);
    sourceBuilder.MapEnum<StatusOS>("status_os_enum", nameTranslator: translator);
    sourceBuilder.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: translator);
    sourceBuilder.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: translator);
    sourceBuilder.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: translator);
    await using var source = sourceBuilder.Build();
    var options = new DbContextOptionsBuilder<AdSyncDbContext>().UseNpgsql(source, o =>
    {
        o.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: translator);
        o.MapEnum<StatusOS>("status_os_enum", nameTranslator: translator);
        o.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: translator);
        o.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: translator);
        o.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: translator);
    }).AddInterceptors(new ReadOnlySaveChangesInterceptor(true)).Options;
    await using var db = new AdSyncDbContext(options);
    Console.WriteLine($"Leitura: {await db.Usuarios.AsNoTracking().CountAsync()} perfis, {await db.Equipamentos.AsNoTracking().CountAsync()} equipamentos, {await db.Pecas.AsNoTracking().CountAsync()} peças, {await db.OrdensServico.AsNoTracking().CountAsync()} ordens, {await db.PlanosPreventivos.AsNoTracking().CountAsync()} planos.");
    _ = await db.Usuarios.AsNoTracking().Take(5).ToListAsync();
    _ = await db.OrdensServico.AsNoTracking().Include(x => x.RegistrosTempo).Take(5).ToListAsync();
    _ = await db.Users.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync();
    _ = await db.Roles.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync();
    Console.WriteLine("PASS: consultas EF, Identity e materialização de enums existentes. Nenhuma gravação solicitada.");
}
catch (Exception exception)
{
    // Connection exceptions can contain host/connection details: never dump them.
    Console.Error.WriteLine($"Falha na validação de leitura ({exception.GetType().Name}). Valores de conexão e detalhes sensíveis omitidos.");
    Environment.ExitCode = 1;
}
