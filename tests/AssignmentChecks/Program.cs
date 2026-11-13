using ADSync.Application.DTOs;
using ADSync.Application.Services;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Infrastructure.Data;
using ADSync.Infrastructure.Identity;
using ADSync.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using ADSync.Infrastructure.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ADSync.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ADSync.Domain.Interfaces;
using ADSync.Api.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;

// Banco descartável com nome único, somente no PostgreSQL portátil de testes.
// Não lê User Secrets nem aceita uma conexão externa.
var database = "adsync_assignment_" + Guid.NewGuid().ToString("N");
const string cluster = "Host=127.0.0.1;Port=55433;Database=postgres;Username=adsync_test";
await using var admin = new NpgsqlConnection(cluster);
await admin.OpenAsync();
await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
try
{
    var connection = new NpgsqlConnectionStringBuilder(cluster) { Database = database }.ConnectionString;
    var translator = new NpgsqlNullNameTranslator();
    var builder = new NpgsqlDataSourceBuilder(connection);
    builder.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: translator);
    builder.MapEnum<StatusOS>("status_os_enum", nameTranslator: translator);
    builder.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: translator);
    builder.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: translator);
    builder.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: translator);
    await using var source = builder.Build();
    var options = new DbContextOptionsBuilder<AdSyncDbContext>().UseNpgsql(source, o =>
    {
        o.MapEnum<TipoUsuario>("tipo_usuario_enum", nameTranslator: translator);
        o.MapEnum<StatusOS>("status_os_enum", nameTranslator: translator);
        o.MapEnum<PrioridadeOS>("prioridade_os_enum", nameTranslator: translator);
        o.MapEnum<TipoRegistroTempo>("tipo_registro_tempo_enum", nameTranslator: translator);
        o.MapEnum<TipoManutencao>("tipo_manutencao_enum", nameTranslator: translator);
    }).Options;
    await using (var setup = new AdSyncDbContext(options))
    {
        await setup.Database.EnsureCreatedAsync();
        await setup.Database.ExecuteSqlRawAsync("CREATE SEQUENCE os_numero_seq");
        // Valida o mesmo script que será revisado para o Supabase, apenas neste banco novo.
        await setup.Database.ExecuteSqlRawAsync("DROP TABLE os_eventos_sincronizados");
        await setup.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("docs/sql/ATENDIMENTO-OFFLINE.sql"));
        await setup.Database.ExecuteSqlRawAsync("DROP TABLE equipamento_operacao_diaria");
        await setup.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("docs/sql/MTBF.sql"));
    }
    await using (var refresh = await source.OpenConnectionAsync()) await refresh.ReloadTypesAsync();
    var owner = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var inactive = Guid.NewGuid(); var equipment = Guid.NewGuid();
    void Check(bool value, string message) { if (!value) throw new Exception(message); }
    CriarOrdemServicoDto Draft(Guid? sync = null) => new() { EquipamentoId = equipment, OperadorId = owner, DescricaoProblema = "Teste atribuição", ClientSyncId = sync };
    OrdemServicoService Service(AdSyncDbContext db) => new(new OrdemServicoRepository(db), new UsuarioRepository(db), new PecaRepository(db), new CausaFalhaRepository(db), new EquipamentoRepository(db));
    async Task<OrdemServicoDto> Open(CriarOrdemServicoDto draft)
    {
        await using var db = new AdSyncDbContext(options);
        return await Service(db).AbrirAsync(draft);
    }
    await using (var db = new AdSyncDbContext(options))
    {
        db.Users.Add(new ApplicationUser { Id = owner, UserName = "owner" });
        db.Usuarios.Add(new Usuario { Id = owner, Nome = "Operador", Email = "owner@example.test", TipoUsuario = TipoUsuario.Operador });
        db.Equipamentos.Add(new Equipamento { Id = equipment, Nome = "Teste", CodigoPatrimonio = "TESTE", Setor = "Teste" });
        await db.SaveChangesAsync();
    }
    var mtbfConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Mtbf:Enabled"] = "true" }).Build();
    var mtbfDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
    await using (var db = new AdSyncDbContext(options))
    {
        var controller = new OperacaoDiariaController(db, mtbfConfig);
        Check((await controller.Salvar(equipment, mtbfDate, new() { HorasOperadas = 8 })).Result is OkObjectResult, "Horas não foram gravadas.");
        Check((await controller.Salvar(equipment, mtbfDate, new() { HorasOperadas = 12 })).Result is OkObjectResult, "Correção de horas falhou.");
        Check(await db.OperacoesDiarias.CountAsync() == 1 && (await db.OperacoesDiarias.SingleAsync()).HorasOperadas == 12, "Reenvio acumulou ou duplicou horas.");
        foreach (var hours in new[] { -1m, 25m, 1.001m })
            Check((await controller.Salvar(equipment, mtbfDate, new() { HorasOperadas = hours })).Result is BadRequestObjectResult, "Horas inválidas aceitas.");
        Check((await controller.Salvar(equipment, mtbfDate.AddDays(2), new())).Result is BadRequestObjectResult, "Data futura aceita.");
        Check((await controller.Salvar(Guid.NewGuid(), mtbfDate, new())).Result is NotFoundObjectResult, "Equipamento inexistente aceito.");
        Check((await controller.Listar(mtbfDate.AddDays(1), mtbfDate)).Result is BadRequestObjectResult, "Consulta invertida aceita.");
        mtbfConfig["Safety:ReadOnly"] = "true";
        Check((await controller.Salvar(equipment, mtbfDate, new())).Result is ObjectResult { StatusCode: 403 }, "Somente leitura permitiu SQL direto.");
        mtbfConfig["Safety:ReadOnly"] = "false"; mtbfConfig["Mtbf:Enabled"] = "false";
        Check((await controller.Salvar(equipment, mtbfDate, new())).Result is ConflictObjectResult, "Recurso desabilitado permitiu escrita.");
        mtbfConfig["Mtbf:Enabled"] = "true";
    }
    await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
    {
        await using var db = new AdSyncDbContext(options);
        await new OperacaoDiariaController(db, mtbfConfig).Salvar(equipment, mtbfDate, new() { HorasOperadas = 10 });
    }));
    await using (var db = new AdSyncDbContext(options))
    {
        Check(await db.OperacoesDiarias.CountAsync() == 1, "Envios concorrentes duplicaram o dia.");
        var result = await new IndicadoresService(new IndicadoresRepository(db, mtbfConfig)).ObterAsync(mtbfDate, mtbfDate, null);
        Check(result.Geral.HorasOperadas == 10 && result.Geral.EquipamentosMtbf == 1 && result.Geral.MtbfHoras is null, "Repositório MTBF não leu horas ou inventou falhas.");
    }
    Console.WriteLine("PASS: script MTBF, lançamento/correção, concorrência, validação, leitura e bloqueios sem Supabase.");
    var unassigned = await Open(Draft());
    await using (var db = new AdSyncDbContext(options))
    {
        var os = await db.OrdensServico.SingleAsync(o => o.Id == unassigned.Id);
        Check(os.TecnicoId is null && os.Status == StatusOS.Aberta, "Sem técnico: ordem deveria permanecer aberta sem responsável.");
        foreach (var id in new[] { a, b, inactive })
        {
            db.Users.Add(new ApplicationUser { Id = id, UserName = id.ToString() });
            db.Usuarios.Add(new Usuario { Id = id, Nome = id == a ? "A" : "B", Email = id + "@example.test", TipoUsuario = TipoUsuario.Tecnico, Ativo = id != inactive });
        }
        await db.SaveChangesAsync();
        foreach (var status in new[] { StatusOS.Aberta, StatusOS.EmAndamento, StatusOS.Pausada })
            db.OrdensServico.Add(new OrdemServico { Id = Guid.NewGuid(), NumeroOS = status.ToString(), EquipamentoId = equipment, OperadorId = owner, TecnicoId = a, Status = status });
        foreach (var status in new[] { StatusOS.Concluida, StatusOS.Cancelada })
            db.OrdensServico.Add(new OrdemServico { Id = Guid.NewGuid(), NumeroOS = status.ToString(), EquipamentoId = equipment, OperadorId = owner, TecnicoId = b, Status = status });
        await db.SaveChangesAsync();
    }
    var selected = await Open(Draft());
    await using (var db = new AdSyncDbContext(options))
    {
        Check((await db.OrdensServico.SingleAsync(o => o.Id == selected.Id)).TecnicoId == b, "Não escolheu o técnico ativo menos ocupado.");
        await db.OrdensServico.ExecuteDeleteAsync();
    }
    Console.WriteLine("PASS: sem técnico mantém aberta; ignora inativos e não técnicos; conta abertas/em andamento/pausadas, exclui concluídas/canceladas.");

    await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Open(Draft())));
    await using (var db = new AdSyncDbContext(options))
    {
        Check(await db.OrdensServico.CountAsync(o => o.TecnicoId == a) == 5 && await db.OrdensServico.CountAsync(o => o.TecnicoId == b) == 5, "Aberturas concorrentes desequilibraram a distribuição.");
    }
    Console.WriteLine("PASS: dez aberturas concorrentes distribuem cinco ordens por técnico.");

    var draft = Draft(Guid.NewGuid()); draft.DataAbertura = DateTimeOffset.UtcNow.AddDays(-1); draft.CriadoOffline = true;
    var repeated = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Open(draft)));
    Check(repeated.Select(o => o.Id).Distinct().Count() == 1, "Reenvios concorrentes duplicaram a ordem.");
    await using (var db = new AdSyncDbContext(options))
    {
        await Service(db).AtribuirTecnicoAsync(repeated[0].Id, a);
    }
    await Open(draft);
    await using (var db = new AdSyncDbContext(options))
    {
        var os = await db.OrdensServico.SingleAsync(o => o.ClientSyncId == draft.ClientSyncId);
        Check(os.TecnicoId == a && Math.Abs((os.DataAbertura - draft.DataAbertura!.Value).TotalMilliseconds) < 1 && os.CriadoOffline, "Reenvio alterou responsável ou data original.");
    }
    draft.OperadorId = Guid.NewGuid();
    try { await Open(draft); throw new Exception("Reenvio de outra conta foi aceito."); }
    catch (AcessoNegadoException) { }
    Console.WriteLine("PASS: reenvios preservam ordem única, data offline e atribuição manual; outra conta é recusada.");

    // Mesmo banco descartável: valida eventos offline com transações e estoque reais.
    var piece = Guid.NewGuid(); var cause = Guid.NewGuid();
    var offlineOrder = await Open(Draft());
    await using (var db = new AdSyncDbContext(options))
    {
        await Service(db).AtribuirTecnicoAsync(offlineOrder.Id, a);
        db.Pecas.Add(new Peca { Id = piece, Codigo = "OFFLINE", Nome = "Teste offline", QuantidadeEstoque = 10 });
        db.CausasFalha.Add(new CausaFalha { Id = cause, Descricao = "Causa offline" });
        await db.SaveChangesAsync();
    }
    var start = DateTimeOffset.UtcNow;
    EventoAtendimentoDto Event(string action, int seconds) => new() { Id = Guid.NewGuid(), OrdemServicoId = offlineOrder.Id, Acao = action, OcorridoEm = start.AddSeconds(seconds) };
    async Task<OrdemServicoDetalheDto> Apply(EventoAtendimentoDto e, Guid? user = null)
    {
        await using var db = new AdSyncDbContext(options);
        return await new AtendimentoOfflineService(db, Service(db)).AplicarAsync(user ?? a, e);
    }
    var begin = Event("iniciar", 0);
    await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Apply(begin)));
    var pause = Event("pausar", 1); pause.Motivo = "Aguardando liberação";
    await Apply(pause);
    await Apply(Event("retomar", 2));
    var usage = Event("pecas", 3); usage.PecaId = piece; usage.Quantidade = 2;
    await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Apply(usage)));
    var insufficient = Event("pecas", 4); insufficient.PecaId = piece; insufficient.Quantidade = 20;
    try { await Apply(insufficient); throw new Exception("Estoque insuficiente aceito."); }
    catch (RegraNegocioException) { }
    var finish = Event("concluir", 5); finish.CausaFalhaId = cause;
    await Apply(finish);
    // Resposta perdida: reenvio do início após conclusão retorna o recibo sem refazer nada.
    Check((await Apply(begin)).Status == "EmAndamento", "Reenvio não devolveu a confirmação original.");
    usage.Quantidade = 1;
    try { await Apply(usage); throw new Exception("Id de evento reutilizado com dados diferentes."); }
    catch (AcessoNegadoException) { }
    try { await Apply(begin, b); throw new Exception("Outra conta acessou o recibo."); }
    catch (AcessoNegadoException) { }
    await using (var db = new AdSyncDbContext(options))
    {
        var result = await Service(db).ObterDetalheAsync(offlineOrder.Id);
        Check(result!.Status == "Concluida" && result.Registros.Count == 4, "Eventos duplicados ou fora de ordem.");
        Check(result.Registros[0].Timestamp == begin.OcorridoEm.AddTicks(-(begin.OcorridoEm.Ticks % 10)), "Hora original não preservada.");
        Check(await db.EventosSincronizados.CountAsync(e => e.OrdemServicoId == offlineOrder.Id) == 5, "Recibos incorretos.");
        Check((await db.Pecas.FindAsync(piece))!.QuantidadeEstoque == 8 && result.Pecas.Single().Quantidade == 2, "Baixa duplicada de estoque.");
    }
    Console.WriteLine("PASS: eventos offline em sequência, horários originais, reenvios concorrentes, estoque sem duplicação e rejeição de dados/conta diferentes.");

    var rollbackOrder = await Open(Draft());
    await using (var db = new AdSyncDbContext(options)) await Service(db).AtribuirTecnicoAsync(rollbackOrder.Id, a);
    var rollbackEvent = new EventoAtendimentoDto { Id = Guid.NewGuid(), OrdemServicoId = rollbackOrder.Id, Acao = "iniciar", OcorridoEm = DateTimeOffset.UtcNow };
    var failingOptions = new DbContextOptionsBuilder<AdSyncDbContext>(options).AddInterceptors(new FailReceipt()).Options;
    await using (var db = new AdSyncDbContext(failingOptions))
    {
        try { await new AtendimentoOfflineService(db, Service(db)).AplicarAsync(a, rollbackEvent); throw new Exception("Falha não simulada."); }
        catch (IOException) { }
    }
    await using (var db = new AdSyncDbContext(options))
    {
        Check((await db.OrdensServico.FindAsync(rollbackOrder.Id))!.Status == StatusOS.Aberta &&
            !await db.EventosSincronizados.AnyAsync(e => e.Id == rollbackEvent.Id), "Falha do recibo deixou uma operação parcialmente gravada.");
        await Service(db).AtribuirTecnicoAsync(rollbackOrder.Id, b);
    }
    try { await Apply(rollbackEvent); throw new Exception("Técnico antigo sincronizou após reatribuição."); }
    catch (AcessoNegadoException) { }
    Console.WriteLine("PASS: falha ao gravar recibo desfaz o evento inteiro; reatribuição bloqueia o técnico anterior.");

    // Executa o job real com contextos distintos, somente neste banco descartável.
    var services = new ServiceCollection();
    services.AddScoped(_ => new AdSyncDbContext(options));
    services.AddScoped<IPlanoPreventivoRepository, PlanoPreventivoRepository>();
    services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
    using var provider = services.BuildServiceProvider();
    var job = new GeracaoPreventivaBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GeracaoPreventivaBackgroundService>.Instance);
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var dueId = Guid.NewGuid(); var inactiveId = Guid.NewGuid(); var futureId = Guid.NewGuid(); var inactiveTechId = Guid.NewGuid();
    await using (var db = new AdSyncDbContext(options))
    {
        foreach (var spec in new[] { (dueId, today.AddDays(-60), true, a), (inactiveId, today, false, a), (futureId, today.AddDays(10), true, a), (inactiveTechId, today, true, inactive) })
            db.PlanosPreventivos.Add(new PlanoPreventivo { Id = spec.Item1, EquipamentoId = equipment, TecnicoResponsavelId = spec.Item4, Descricao = "Revisão preventiva", PeriodicidadeDias = 30, ProximaExecucao = spec.Item2, Ativo = spec.Item3 });
        await db.SaveChangesAsync();
    }
    var generated = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => job.GerarPendentesAsync()));
    Check(generated.Sum() == 1, "Rodadas concorrentes duplicaram a preventiva.");
    await using (var db = new AdSyncDbContext(options))
    {
        var order = await db.OrdensServico.SingleAsync(o => o.PlanoPreventivoId == dueId);
        Check(order.TecnicoId == a && order.TipoManutencao == TipoManutencao.Preventiva, "Preventiva sem tipo/responsável correto.");
        Check((await db.PlanosPreventivos.FindAsync(dueId))!.ProximaExecucao == today.AddDays(-30), "Data não avançou exatamente uma periodicidade.");
        Check(!await db.OrdensServico.AnyAsync(o => o.PlanoPreventivoId == inactiveId || o.PlanoPreventivoId == futureId || o.PlanoPreventivoId == inactiveTechId), "Gerou plano inativo/futuro ou de técnico inativo.");
        order.Status = StatusOS.Pausada;
        await db.SaveChangesAsync();
    }
    Check(await job.GerarPendentesAsync() == 0, "Ordem pausada não impediu nova geração.");
    await using (var db = new AdSyncDbContext(options))
    {
        var order = await db.OrdensServico.SingleAsync(o => o.PlanoPreventivoId == dueId);
        order.Status = StatusOS.Concluida;
        await db.SaveChangesAsync();
    }
    Check(await job.GerarPendentesAsync() == 1 && await job.GerarPendentesAsync() == 0, "Conclusão não liberou próxima geração ou houve duplicação.");
    Console.WriteLine("PASS: job preventivo concorrente gera uma única ordem; respeita inativos, vencimento, responsável, pausa e conclusão; avança a data sem duplicar.");
}

finally
{
    // O único alvo é o nome aleatório criado por esta execução, neste cluster local.
    if (database.StartsWith("adsync_assignment_") && database.Length == 50)
    {
        await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

sealed class FailReceipt : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<EventoSincronizado>().Any(e => e.State == EntityState.Added))
            throw new IOException("Falha de recibo simulada.");
        return ValueTask.FromResult(result);
    }
}
