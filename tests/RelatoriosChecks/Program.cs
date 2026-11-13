using ADSync.Api.Controllers;
using ADSync.Application.DTOs;
using ADSync.Application.Services;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Dados isolados em memória: não carrega User Secrets nem acessa bancos.
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
DateTimeOffset At(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
var clock = new FixedClock(At("2026-12-05T12:00:00Z"));
var e = new Equipamento { Id = Guid.NewGuid(), Nome = "Esteira", CodigoPatrimonio = "E-01", Setor = " Produção " };
var b = new Equipamento { Id = Guid.NewGuid(), Nome = "Compressor", Setor = "produção" };
var part = new Peca { Id = Guid.NewGuid(), Codigo = "P-01", Nome = "Rolamento" };
OrdemServico Done(Equipamento equipment, string end, TipoManutencao type = TipoManutencao.Corretiva) => new()
{
    Id = Guid.NewGuid(), NumeroOS = Guid.NewGuid().ToString()[..8], Equipamento = equipment, EquipamentoId = equipment.Id,
    DataAbertura = At("2026-10-31T10:00:00Z"), DataConclusao = At(end), Status = StatusOS.Concluida, TipoManutencao = type,
    Tecnico = new Usuario { Nome = "Técnico de teste" }, CausaFalha = new CausaFalha { Descricao = "Desgaste" }, ObservacoesTecnicas = "Substituição concluída."
};
var paused = Done(e, "2026-11-01T09:00:00Z");
paused.RegistrosTempo = [
    new() { TipoRegistro = TipoRegistroTempo.Inicio, Timestamp = At("2026-11-01T08:00:00Z") },
    new() { TipoRegistro = TipoRegistroTempo.Pausa, Timestamp = At("2026-11-01T08:20:00Z") },
    new() { TipoRegistro = TipoRegistroTempo.Retomada, Timestamp = At("2026-11-01T08:35:00Z") },
    new() { TipoRegistro = TipoRegistroTempo.Conclusao, Timestamp = At("2026-11-01T09:00:00Z") }
];
paused.PecasUtilizadas = [new() { PecaId = part.Id, Peca = part, Quantidade = 3 }];
var zero = Done(e, "2026-11-30T23:59:59Z");
zero.RegistrosTempo = [new() { TipoRegistro = TipoRegistroTempo.Inicio, Timestamp = zero.DataConclusao!.Value }, new() { TipoRegistro = TipoRegistroTempo.Conclusao, Timestamp = zero.DataConclusao.Value }];
zero.PecasUtilizadas = [new() { PecaId = part.Id, Peca = part, Quantidade = 2 }];
var prior = Done(e, "2026-10-31T23:59:59Z");
var boundary = Done(e, "2026-12-01T00:00:00Z");
var cancelled = Done(e, "2026-11-15T10:00:00Z"); cancelled.Status = StatusOS.Cancelada;
var urgent = new OrdemServico { Id = Guid.NewGuid(), NumeroOS = "URG-01", Status = StatusOS.Pausada, Prioridade = PrioridadeOS.Critica, DataAbertura = At("2026-10-01T00:00:00Z") };
var repo = new MemoryReports
{
    Orders = [paused, zero, prior, boundary, cancelled, urgent, Done(e, "2026-11-15T10:00:00Z", TipoManutencao.Preventiva), Done(b, "2026-11-16T10:00:00Z"), Done(b, "2026-11-17T10:00:00Z")],
    Plans = [new() { Id = Guid.NewGuid(), Equipamento = e, EquipamentoId = e.Id, Descricao = "Inspeção", ProximaExecucao = new(2026, 12, 1) }, new() { Equipamento = e, EquipamentoId = e.Id, ProximaExecucao = new(2027, 1, 5) }],
    Stock = [new() { Id = Guid.NewGuid(), Nome = "Peça crítica", Codigo = "C-01", Critica = true }]
};
var service = new RelatorioMensalService(repo, clock);
var result = await service.ObterAsync(2026, 11);
Check(result.DataInicio == new DateOnly(2026, 11, 1) && result.DataFim == new DateOnly(2026, 11, 30) && !result.MesEmAndamento, "Mês civil incorreto.");
Check(result.Resumo.Concluidas == 5 && result.MesAnterior!.Concluidas == 1, "Limites UTC ou mês anterior incorretos.");
Check(result.Resumo.Corretivas == 4 && result.Resumo.Preventivas == 1 && result.Resumo.PercentualPreventiva == 20, "Contagem ou percentual incorretos.");
Check(result.Resumo.HorasTrabalhadas == .75 && result.Resumo.MttrMinutos == 22.5 && result.Resumo.AmostraMttr == 2, "Pausas, tempo zero ou MTTR incorretos.");
Check(result.Resumo.OrdensSemTempoValido == 3 && result.Resumo.OrdensComTempoValido == 2, "Ausência de registros tratada como tempo zero.");
Check(result.Atividades.Count == 5 && result.Atividades[0].Observacoes == paused.ObservacoesTecnicas && result.Atividades[0].Causa == "Desgaste", "Histórico sem diagnóstico ou observações.");
Check(result.Pecas.Single().Quantidade == 5 && result.Setores.Single().Corretivas == 4, "Peças ou normalização de setor incorretas.");
Check(result.AgendaAtual.Count == 1 && result.AgendaAtual[0].Vencida, "Agenda não usa emissão + 30 dias.");
Check(result.PendentesAtuais == 1 && result.Recomendacoes.Any(r => r.Evidencia.Contains("URG-01")), "Pendências atuais confundidas com conclusões do mês.");
Check(result.Recomendacoes.Any(r => r.Acao.Contains("periodicidade")) && result.Recomendacoes.Any(r => r.Acao.Contains("necessidade de um plano")), "Recorrências não consideraram planos existentes.");
Check(result.Recomendacoes.Any(r => r.Titulo.Contains("reposição")) && result.Recomendacoes.Any(r => r.Prioridade == "Dados"), "Alertas de estoque ou qualidade ausentes.");
Console.WriteLine("PASS: mês civil UTC, comparação, histórico técnico, pausas, zero válido, peças, setores e recomendações justificadas.");

// Mês corrente: eventos futuros não entram. Recomendações continuam sendo atuais.
repo.Orders.Add(Done(e, "2026-12-05T12:00:01Z"));
var partial = await service.ObterAsync(2026, 12);
Check(partial.MesEmAndamento && partial.Resumo.Concluidas == 1 && repo.LastEnd == clock.GetUtcNow(), "Mês parcial incluiu futuro.");
var february = await service.ObterAsync(2024, 2);
Check(february.DataFim == new DateOnly(2024, 2, 29) && february.Resumo.PercentualPreventiva is null && february.Resumo.HorasTrabalhadas is null, "Ano bissexto ou ausência de dados incorretos.");
await service.ObterAsync(2026, 1);
Check(repo.LastStart == At("2025-12-01T00:00:00Z"), "Comparação de janeiro não passou para o ano anterior.");
var first = await service.ObterAsync(1, 1);
Check(first.MesAnterior is null, "Primeiro mês do calendário extrapolou datas.");
var before = repo.Calls;
var controller = new RelatoriosController(service);
foreach (var (year, month) in new[] { (0, 1), (9999, 1), (2026, 0), (2026, 13), (2027, 1) })
    Check((await controller.Mensal(year, month)).Result is BadRequestObjectResult, "Período inválido não retornou 400.");
Check(repo.Calls == before, "Período inválido chegou ao repositório.");
Check(typeof(RelatoriosController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Gestor", "Relatório não exige Gestor.");
Check(typeof(RelatoriosController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>().Single().NoStore, "Relatório financeiro permite cache HTTP.");
Check((await controller.Mensal(2026, 11)).Result is OkObjectResult { Value: RelatorioMensalDto }, "Contrato da consulta mensal inválido.");
Console.WriteLine("PASS: mês parcial, futuro, ano bissexto, janeiro, datas extremas, HTTP 400, acesso Gestor e cache desabilitado.");

sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
sealed class MemoryReports : IRelatorioMensalRepository
{
    public List<OrdemServico> Orders { get; set; } = [];
    public List<PlanoPreventivo> Plans { get; set; } = [];
    public List<Peca> Stock { get; set; } = [];
    public DateTimeOffset LastStart, LastEnd;
    public int Calls;
    public Task<List<OrdemServico>> ListarConcluidasAsync(DateTimeOffset inicio, DateTimeOffset fim)
    {
        Calls++; LastStart = inicio; LastEnd = fim;
        return Task.FromResult(Orders.Where(o => o.Status == StatusOS.Concluida && o.DataConclusao >= inicio && o.DataConclusao < fim).ToList());
    }
    public Task<int> ContarAberturasAsync(DateTimeOffset inicio, DateTimeOffset fim) => Task.FromResult(Orders.Count(o => o.DataAbertura >= inicio && o.DataAbertura < fim));
    public Task<List<OrdemServico>> ListarPendentesAsync(DateTimeOffset agora) => Task.FromResult(Orders.Where(o => o.DataAbertura <= agora && o.Status is StatusOS.Aberta or StatusOS.EmAndamento or StatusOS.Pausada).ToList());
    public Task<List<PlanoPreventivo>> ListarPlanosAtivosAsync() => Task.FromResult(Plans);
    public Task<List<Peca>> ListarPecasCriticasSemEstoqueAsync() => Task.FromResult(Stock);
}
