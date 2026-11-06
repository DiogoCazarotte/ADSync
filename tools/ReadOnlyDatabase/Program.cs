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
    if (args.Contains("--preventivas"))
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plans = await db.PlanosPreventivos.AsNoTracking()
            .Include(p => p.Equipamento).Include(p => p.TecnicoResponsavel).ToListAsync();
        var blocked = await db.OrdensServico.AsNoTracking()
            .Where(o => o.PlanoPreventivoId != null && (o.Status == StatusOS.Aberta || o.Status == StatusOS.EmAndamento || o.Status == StatusOS.Pausada))
            .Select(o => o.PlanoPreventivoId!.Value).Distinct().ToListAsync();
        var due = plans.Where(p => p.Ativo && p.ProximaExecucao <= today).ToList();
        var eligible = due.Where(p => p.Equipamento?.Ativo == true && p.TecnicoResponsavel?.Ativo == true && !blocked.Contains(p.Id)).ToList();
        Console.WriteLine($"Referência UTC: {today:yyyy-MM-dd}. Total: {plans.Count}; ativos: {plans.Count(p => p.Ativo)}; ativos com data vencida/hoje: {due.Count}.");
        Console.WriteLine($"Elegíveis na próxima rodada: {eligible.Count}; vencidos bloqueados por ordem aberta/andamento/pausa: {due.Count(p => blocked.Contains(p.Id))}; vencidos com equipamento/técnico inativo ou ausente: {due.Count(p => p.Equipamento?.Ativo != true || p.TecnicoResponsavel?.Ativo != true)}.");
        foreach (var plan in eligible.OrderBy(p => p.ProximaExecucao))
            Console.WriteLine($"Plano {plan.Id}: próxima {plan.ProximaExecucao:yyyy-MM-dd}; periodicidade {plan.PeriodicidadeDias} dias; data após gerar {plan.ProximaExecucao.AddDays(plan.PeriodicidadeDias):yyyy-MM-dd}.");
        Console.WriteLine("Somente consulta; agendador e dados preservados.");
        return;
    }
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
