using ADSync.Api.Controllers;
using ADSync.Application.DTOs;
using ADSync.Application.Services;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

// Apenas dados em memória. Não lê credenciais nem acessa qualquer banco.
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
DateTimeOffset At(string date) => DateTimeOffset.Parse(date + "Z", System.Globalization.CultureInfo.InvariantCulture);
var a = new Equipamento { Id = Guid.NewGuid(), Nome = "Esteira", Setor = " Produção " };
var aLower = new Equipamento { Id = a.Id, Nome = a.Nome, Setor = "produção" };
var b = new Equipamento { Id = Guid.NewGuid(), Nome = "Compressor", Setor = "Expedição" };
OrdemServico Order(Equipamento? e, string open, string done, TipoManutencao type = TipoManutencao.Corretiva) => new()
{
    Id = Guid.NewGuid(), Equipamento = e, EquipamentoId = e?.Id ?? Guid.Empty,
    Status = StatusOS.Concluida, TipoManutencao = type, DataAbertura = At(open), DataConclusao = At(done)
};
void Event(OrdemServico os, TipoRegistroTempo type, string at) => os.RegistrosTempo.Add(new OsRegistroTempo { TipoRegistro = type, Timestamp = At(at) });
var paused = Order(a, "2026-10-30T10:00:00", "2026-11-01T09:00:00");
Event(paused, TipoRegistroTempo.Inicio, "2026-11-01T08:00:00");
Event(paused, TipoRegistroTempo.Pausa, "2026-11-01T08:20:00");
Event(paused, TipoRegistroTempo.Retomada, "2026-11-01T08:35:00");
Event(paused, TipoRegistroTempo.Conclusao, "2026-11-01T09:00:00");
var missing = Order(aLower, "2026-11-03T10:00:00", "2026-11-05T10:00:00");
var zero = Order(b, "2026-11-07T10:00:00", "2026-11-08T10:00:00");
Event(zero, TipoRegistroTempo.Inicio, "2026-11-08T10:00:00");
Event(zero, TipoRegistroTempo.Conclusao, "2026-11-08T10:00:00");
var prior = Order(a, "2026-10-21T10:00:00", "2026-10-22T10:10:00");
Event(prior, TipoRegistroTempo.Inicio, "2026-10-22T10:00:00");
Event(prior, TipoRegistroTempo.Conclusao, "2026-10-22T10:10:00");
var cancelled = Order(a, "2026-11-01T10:00:00", "2026-11-02T10:00:00");
cancelled.Status = StatusOS.Cancelada;
var repo = new MemoryIndicators([
    paused, missing, zero, prior, cancelled,
    Order(aLower, "2026-11-06T09:00:00", "2026-11-06T10:00:00", TipoManutencao.Preventiva),
    Order(null, "2026-11-10T22:00:00", "2026-11-10T23:59:59", TipoManutencao.Preventiva),
    Order(a, "2026-10-31T22:00:00", "2026-10-31T23:59:59", TipoManutencao.Preventiva),
    Order(a, "2026-11-10T23:00:00", "2026-11-11T00:00:00"),
    Order(a, "2026-10-20T10:00:00", "2026-10-21T23:59:59")
]);
var service = new IndicadoresService(repo);
var start = new DateOnly(2026, 11, 1); var end = new DateOnly(2026, 11, 10);
var result = await service.ObterAsync(start, end, null);
Check(result.Geral.TotalConcluidas == 5 && result.Geral.TotalCorretivas == 3 && result.Geral.TotalPreventivas == 2, "Contagens ou limites inclusivos incorretos.");
Check(result.Geral.PercentualPreventiva == 40 && result.Geral.MttrMinutos == 22.5, "Participação ou MTTR com pausas e zero válido incorretos.");
Check(result.Geral.AmostraMttr == 2 && result.Geral.CorretivasSemTempo == 1, "Registros ausentes entraram no MTTR.");
Check(result.Geral.MtbfDias == 4 && result.Geral.AmostraIntervalos == 1, "Intervalos misturaram equipamentos ou foram filtrados pela abertura.");
Check(result.DataInicioAnterior == new DateOnly(2026, 10, 22) && result.DataFimAnterior == new DateOnly(2026, 10, 31) && result.Anterior!.TotalConcluidas == 2, "Período anterior não tem a mesma duração ou inclui a fronteira atual.");
Check(result.Evolucao.Count == 10 && result.Evolucao[1].Corretivas == 0 && result.Evolucao[^1].Preventivas == 1, "Evolução omitiu zeros ou perdeu o último dia.");
Check(result.Evolucao.Sum(p => p.Preventivas + p.Corretivas) == result.Geral.TotalConcluidas && !result.EvolucaoMensal, "Gráfico não reconcilia com o total.");
Check(result.PorSetor.Count == 3 && result.PorSetor.Sum(s => s.TotalConcluidas) == 5, "Setores não agrupados ou total divergente.");
Check(result.Equipamentos.Count == 2 && result.Equipamentos[0].EquipamentoId == a.Id && result.Equipamentos[0].Corretivas == 2, "Ranking não usa equipamento e corretivas.");
Console.WriteLine("PASS: contagens, limites UTC, pausas, amostras, comparação, dias sem dados, setores e ranking.");

var filtered = await service.ObterAsync(start, end, " PRODUÇÃO ");
Check(filtered.Geral.TotalConcluidas == 3 && filtered.PorSetor.Count == 1 && filtered.Anterior!.TotalConcluidas == 2, "Filtro de setor não foi normalizado ou não vale para a comparação.");
Check(filtered.Geral.MttrMinutos == 45 && filtered.SetoresDisponiveis.Count == 3, "Amostra filtrada ou opções de setor incorretas.");
var unset = await service.ObterAsync(start, end, "(sem setor)");
Check(unset.Geral.TotalConcluidas == 1 && unset.Geral.PercentualPreventiva == 100 && unset.Geral.MttrMinutos is null, "Filtro sem setor ou ausência de amostra incorretos.");
var empty = await service.ObterAsync(start, end, "Inexistente");
Check(empty.Geral.TotalConcluidas == 0 && empty.Geral.PercentualPreventiva is null && empty.Geral.MttrMinutos is null && empty.Geral.MtbfDias is null && empty.Evolucao.Count == 10, "Ausência de dados virou zero ou perdeu a série.");
var month = await service.ObterAsync(new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15), null);
Check(month.EvolucaoMensal && month.Evolucao.Count == 2 && month.Evolucao.Sum(p => p.Preventivas + p.Corretivas) == month.Geral.TotalConcluidas, "Agregação mensal não respeita o período parcial.");
Console.WriteLine("PASS: filtros nos dois períodos, sem setor, período vazio e meses parciais.");

foreach (var scenario in new[] { "incompleto", "duplicado", "fora da conclusão", "sem início" })
{
    var invalid = Order(a, "2026-11-02T08:00:00", "2026-11-02T10:00:00");
    if (scenario != "sem início") Event(invalid, TipoRegistroTempo.Inicio, "2026-11-02T09:00:00");
    if (scenario == "duplicado") Event(invalid, TipoRegistroTempo.Inicio, "2026-11-02T09:05:00");
    if (scenario != "incompleto") Event(invalid, TipoRegistroTempo.Conclusao, scenario == "fora da conclusão" ? "2026-11-02T10:01:00" : "2026-11-02T10:00:00");
    var invalidResult = await new IndicadoresService(new MemoryIndicators([invalid])).ObterAsync(start, end, null);
    Check(invalidResult.Geral.MttrMinutos is null && invalidResult.Geral.CorretivasSemTempo == 1, $"Sequência {scenario} gerou MTTR.");
}
var min = await new IndicadoresService(new MemoryIndicators([])).ObterAsync(DateOnly.MinValue, DateOnly.MinValue, null);
Check(min.Anterior is null && min.DataInicioAnterior is null, "Data mínima causou underflow.");
var max = await new IndicadoresService(new MemoryIndicators([])).ObterAsync(new DateOnly(9999, 11, 1), new DateOnly(9999, 12, 30), null);
Check(max.Evolucao.Count == 2, "Último mês causou overflow.");
var controller = new IndicadoresController(service);
Check((await controller.Obter(end, start, null)).Result is BadRequestObjectResult, "Período invertido não retornou 400.");
Check((await controller.Obter(null, DateOnly.MaxValue, null)).Result is BadRequestObjectResult, "Última data não retornou 400.");
var defaultRange = (IndicadoresDto)((OkObjectResult)(await controller.Obter(null, end, null)).Result!).Value!;
Check(defaultRange.DataFim.DayNumber - defaultRange.DataInicio.DayNumber == 29, "Padrão não tem exatamente 30 dias inclusivos.");
Console.WriteLine("PASS: registros incompletos/inválidos, extremos de datas e validação HTTP.");

// MTBF usa falhas abertas na janela, inclusive não concluídas; horas e falhas
// de equipamentos com lacunas nunca entram parcialmente na amostra.
var mtbfA = new Equipamento { Id = Guid.NewGuid(), Nome = "Costura", Setor = "Costura" };
var mtbfB = new Equipamento { Id = Guid.NewGuid(), Nome = "Ensacadeira", Setor = "Envase" };
var mtbfC = new Equipamento { Id = Guid.NewGuid(), Nome = "Sem registros", Setor = "Envase" };
var failure = Order(mtbfA, "2026-11-01T12:00:00", "2026-11-03T12:00:00");
failure.Status = StatusOS.EmAndamento;
var excludedFailure = Order(mtbfB, "2026-11-02T12:00:00", "2026-11-03T12:00:00");
var cancelledFailure = Order(mtbfA, "2026-11-02T12:00:00", "2026-11-03T12:00:00");
cancelledFailure.Status = StatusOS.Cancelada;
var mtbfRepo = new MemoryIndicators([failure, excludedFailure, cancelledFailure,
    Order(mtbfA, "2026-11-02T12:00:00", "2026-11-03T12:00:00", TipoManutencao.Preventiva)])
{
    MtbfHabilitado = true, Equipamentos = [mtbfA, mtbfB, mtbfC],
    Operacoes = [
        new() { EquipamentoId = mtbfA.Id, Data = new(2026, 11, 1), HorasOperadas = 8 },
        new() { EquipamentoId = mtbfA.Id, Data = new(2026, 11, 2), HorasOperadas = 0 },
        new() { EquipamentoId = mtbfB.Id, Data = new(2026, 11, 1), HorasOperadas = 24 },
        new() { EquipamentoId = mtbfA.Id, Data = new(2026, 10, 30), HorasOperadas = 10 },
        new() { EquipamentoId = mtbfA.Id, Data = new(2026, 10, 31), HorasOperadas = 10 }
    ]
};
var mtbf = await new IndicadoresService(mtbfRepo).ObterAsync(new(2026, 11, 1), new(2026, 11, 2), null);
Check(mtbf.MtbfHabilitado && mtbf.Geral.MtbfHoras == 8 && mtbf.Geral.HorasOperadas == 8 && mtbf.Geral.FalhasMtbf == 1,
    "MTBF incluiu preventiva, cancelada, equipamento incompleto ou ignorou falha em andamento.");
Check(mtbf.Geral.EquipamentosMtbf == 1 && mtbf.Geral.EquipamentosSemCobertura == 2, "Cobertura não distingue zero de ausência de lançamento.");
Check(mtbf.Geral.TotalConcluidas == 0 && mtbf.Geral.MtbfHoras.HasValue, "MTBF depende indevidamente de conclusões.");
Check(mtbf.Anterior!.HorasOperadas == 20 && mtbf.Anterior.MtbfHoras is null, "Período sem falhas fabricou MTBF.");
var mtbfSector = await new IndicadoresService(mtbfRepo).ObterAsync(new(2026, 11, 1), new(2026, 11, 2), " ENVASE ");
Check(mtbfSector.Geral.MtbfHoras is null && mtbfSector.Geral.HorasOperadas == 0 && mtbfSector.Geral.EquipamentosSemCobertura == 2,
    "Filtro MTBF deixou horas ou falhas de outro setor entrarem na amostra.");
mtbfRepo.Operacoes[0].HorasOperadas = 0;
var noHours = await new IndicadoresService(mtbfRepo).ObterAsync(new(2026, 11, 1), new(2026, 11, 2), null);
Check(noHours.Geral.MtbfHoras is null && noHours.Geral.FalhasMtbf == 1, "Falha sem horas gerou MTBF zero.");
Console.WriteLine("PASS: MTBF por operação, cobertura completa, zero explícito, falhas abertas, filtros, comparação e ausência de base.");

sealed class MemoryIndicators(List<OrdemServico> items) : IIndicadoresRepository
{
    public bool MtbfHabilitado { get; init; }
    public List<Equipamento> Equipamentos { get; init; } = [];
    public List<OperacaoDiaria> Operacoes { get; init; } = [];
    public Task<List<Equipamento>> ListarEquipamentosMtbfAsync() => Task.FromResult(Equipamentos);
    public Task<List<OperacaoDiaria>> ListarOperacoesAsync(DateOnly inicio, DateOnly fim) => Task.FromResult(Operacoes.Where(o => o.Data >= inicio && o.Data <= fim).ToList());
    public Task<List<OrdemServico>> ListarFalhasAsync(DateTimeOffset inicio, DateTimeOffset fim) => Task.FromResult(items.Where(o => o.TipoManutencao == TipoManutencao.Corretiva && o.Status != StatusOS.Cancelada && o.DataAbertura >= inicio && o.DataAbertura < fim).ToList());
    public Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset start, DateTimeOffset end) => Task.FromResult(items.Where(o =>
        o.Status == StatusOS.Concluida && o.DataConclusao >= start && o.DataConclusao < end).ToList());
}
