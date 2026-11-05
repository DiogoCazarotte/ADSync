using ADSync.Domain.Enums;
using ADSync.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__AdSync")
    ?? throw new InvalidOperationException("Defina ConnectionStrings__AdSync no processo.");
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (parsed.Host != "127.0.0.1" || parsed.Database != "adsync_dev")
    throw new InvalidOperationException("Permitido somente Host=127.0.0.1 e Database=adsync_dev.");
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
}).Options;
await using var db = new AdSyncDbContext(options);
await db.Database.EnsureCreatedAsync();
await db.Database.ExecuteSqlRawAsync("CREATE SEQUENCE IF NOT EXISTS os_numero_seq");
Console.WriteLine("Modelo demonstrativo verificado; dados preservados. Esquemas antigos não são atualizados.");
