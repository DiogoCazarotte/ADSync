using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

public class RelatorioMensalService(IRelatorioMensalRepository repo, TimeProvider clock) : IRelatorioMensalService
{
    public async Task<RelatorioMensalDto> ObterAsync(int ano, int mes)
    {
        var agora = clock.GetUtcNow();
        var hoje = DateOnly.FromDateTime(agora.UtcDateTime);
        if (ano is < 1 or > 9998 || mes is < 1 or > 12)
            throw new ArgumentException("Informe um ano entre 1 e 9998 e um mês entre 1 e 12.");
        var inicio = new DateOnly(ano, mes, 1);
        if (inicio > hoje) throw new ArgumentException("Selecione o mês atual ou um mês anterior.");
        var fimExclusivo = inicio.AddMonths(1);
        var anterior = inicio == DateOnly.MinValue ? (DateOnly?)null : inicio.AddMonths(-1);
        var limite = Utc(fimExclusivo) < agora ? Utc(fimExclusivo) : agora;
        // O mês corrente é parcial, sem incluir registros com datas futuras.
        var historico = await repo.ListarConcluidasAsync(Utc(anterior ?? inicio), limite);
        var atuais = historico.Where(o => o.DataConclusao >= Utc(inicio)).ToList();
        var previas = historico.Where(o => o.DataConclusao < Utc(inicio)).ToList();
        var pendentes = await repo.ListarPendentesAsync(agora);
        var planos = await repo.ListarPlanosAtivosAsync();
        var estoque = await repo.ListarPecasCriticasSemEstoqueAsync();
        var atividades = atuais.OrderBy(o => o.DataConclusao).ThenBy(o => o.NumeroOS).Select(o => new AtividadeMensalDto
        {
            Id = o.Id, Numero = o.NumeroOS, Equipamento = o.Equipamento?.Nome ?? "Equipamento não informado",
            Patrimonio = o.Equipamento?.CodigoPatrimonio ?? "", Setor = Setor(o),
            Tipo = o.TipoManutencao.ToString(), Tecnico = o.Tecnico?.Nome ?? "Não informado",
            ConcluidaEm = o.DataConclusao!.Value, Descricao = o.DescricaoProblema,
            Causa = o.CausaFalha?.Descricao, Observacoes = o.ObservacoesTecnicas,
            MinutosTrabalhados = TempoValido(o), Pecas = Consumo(o.PecasUtilizadas)
        }).ToList();
        var resultado = new RelatorioMensalDto
        {
            DataInicio = inicio, DataFim = fimExclusivo.AddDays(-1), GeradoEm = agora,
            MesEmAndamento = hoje < fimExclusivo,
            OrdensAbertasNoMes = await repo.ContarAberturasAsync(Utc(inicio), limite),
            Resumo = Resumo(atuais), MesAnterior = anterior.HasValue ? Resumo(previas) : null,
            Atividades = atividades, PendentesAtuais = pendentes.Count,
            Pecas = Consumo(atuais.SelectMany(o => o.PecasUtilizadas)),
            Setores = atuais.GroupBy(Setor, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key).Select(g => new SetorMensalDto
            {
                Setor = g.Key, Corretivas = g.Count(o => o.TipoManutencao == TipoManutencao.Corretiva),
                Preventivas = g.Count(o => o.TipoManutencao == TipoManutencao.Preventiva)
            }).ToList(),
            AgendaAtual = planos.Where(p => p.ProximaExecucao <= hoje.AddDays(30))
                .OrderBy(p => p.ProximaExecucao).ThenBy(p => p.Equipamento?.Nome).Select(p => new AgendaPreventivaDto
                {
                    Equipamento = p.Equipamento?.Nome ?? "Não informado", Tarefa = p.Descricao,
                    Responsavel = p.TecnicoResponsavel?.Nome ?? "Não informado", ProximaExecucao = p.ProximaExecucao,
                    Vencida = p.ProximaExecucao < hoje
                }).ToList()
        };
        foreach (var grupo in atuais.Where(o => o.TipoManutencao == TipoManutencao.Corretiva && o.Equipamento?.Ativo == true)
            .GroupBy(o => o.EquipamentoId).Where(g => g.Count() >= 2).OrderByDescending(g => g.Count()).ThenBy(g => g.Key))
        {
            var e = grupo.First().Equipamento!;
            var possuiPlano = planos.Any(p => p.EquipamentoId == e.Id);
            resultado.Recomendacoes.Add(new()
            {
                Prioridade = "Atenção", Titulo = $"Investigar recorrência: {e.Nome}",
                Evidencia = $"{grupo.Count()} corretivas concluídas no mês selecionado. Patrimônio: {e.CodigoPatrimonio}.",
                Acao = possuiPlano ? "Revisar diagnóstico e periodicidade do plano ativo antes de definir nova intervenção."
                    : "Avaliar a causa das falhas e a necessidade de um plano preventivo para este equipamento."
            });
        }
        var urgentes = pendentes.Where(o => o.Prioridade is PrioridadeOS.Alta or PrioridadeOS.Critica).ToList();
        if (urgentes.Count > 0) resultado.Recomendacoes.Insert(0, new()
        {
            Prioridade = "Alta", Titulo = "Priorizar chamados de alta criticidade",
            Evidencia = $"{urgentes.Count} O.S. de prioridade alta ou crítica ainda pendentes na geração: {string.Join(", ", urgentes.OrderBy(o => o.DataAbertura).Take(10).Select(o => o.NumeroOS))}.",
            Acao = "Confirmar o impacto operacional, o responsável e a programação dos atendimentos."
        });
        var vencidos = resultado.AgendaAtual.Count(p => p.Vencida);
        if (vencidos > 0) resultado.Recomendacoes.Add(new()
        {
            Prioridade = "Alta", Titulo = "Revisar datas de planos preventivos",
            Evidencia = $"{vencidos} plano(s) ativo(s) com próxima geração anterior a {hoje:dd/MM/yyyy} UTC.",
            Acao = "Conferir ordens já geradas, disponibilidade do responsável e funcionamento do agendador. A data de geração vencida não comprova atraso na execução."
        });
        foreach (var peca in estoque) resultado.Recomendacoes.Add(new()
        {
            Prioridade = "Alta", Titulo = $"Avaliar reposição: {peca.Nome}",
            Evidencia = $"Peça crítica {peca.Codigo} com estoque atual de {peca.QuantidadeEstoque} unidade(s).",
            Acao = "Conferir demanda e prazo de fornecimento antes de programar intervenções que dependam desta peça."
        });
        if (resultado.Resumo.OrdensSemTempoValido > 0) resultado.Recomendacoes.Add(new()
        {
            Prioridade = "Dados", Titulo = "Completar a qualidade dos registros de atendimento",
            Evidencia = $"{resultado.Resumo.OrdensSemTempoValido} O.S. concluída(s) sem sequência válida de registros de tempo.",
            Acao = "Revisar o registro de início, pausas e conclusão. Ausência de tempo não significa atendimento instantâneo."
        });
        return resultado;
    }

    private static DateTimeOffset Utc(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    private static string Setor(OrdemServico o) => string.IsNullOrWhiteSpace(o.Equipamento?.Setor) ? "(sem setor)" : o.Equipamento.Setor.Trim();
    private static List<ConsumoMensalDto> Consumo(IEnumerable<OsPecaUtilizada> pecas) => pecas.GroupBy(p => p.PecaId)
        .Select(g => new ConsumoMensalDto { PecaId = g.Key, Codigo = g.First().Peca?.Codigo ?? "",
            Nome = g.First().Peca?.Nome ?? "Peça não informada", Quantidade = g.Sum(p => p.Quantidade) })
        .OrderByDescending(p => p.Quantidade).ThenBy(p => p.Nome).ToList();

    private static ResumoMensalDto Resumo(List<OrdemServico> ordens)
    {
        var tempos = ordens.Select(o => (Ordem: o, Tempo: TempoValido(o))).Where(t => t.Tempo.HasValue).ToList();
        var corretivas = tempos.Where(t => t.Ordem.TipoManutencao == TipoManutencao.Corretiva).ToList();
        var preventivas = ordens.Count(o => o.TipoManutencao == TipoManutencao.Preventiva);
        return new()
        {
            Concluidas = ordens.Count, Preventivas = preventivas, Corretivas = ordens.Count - preventivas,
            PercentualPreventiva = ordens.Count > 0 ? Math.Round(100.0 * preventivas / ordens.Count, 1) : null,
            HorasTrabalhadas = tempos.Count > 0 ? Math.Round(tempos.Sum(t => t.Tempo!.Value) / 60, 2) : null,
            OrdensComTempoValido = tempos.Count, OrdensSemTempoValido = ordens.Count - tempos.Count,
            MttrMinutos = corretivas.Count > 0 ? Math.Round(corretivas.Average(t => t.Tempo!.Value), 1) : null,
            AmostraMttr = corretivas.Count
        };
    }

    private static double? TempoValido(OrdemServico os)
    {
        DateTimeOffset? trecho = null;
        double total = 0;
        bool iniciou = false, concluiu = false;
        foreach (var r in os.RegistrosTempo.OrderBy(r => r.Timestamp))
        {
            if (r.Timestamp < os.DataAbertura || r.Timestamp > os.DataConclusao) return null;
            switch (r.TipoRegistro)
            {
                case TipoRegistroTempo.Inicio:
                    if (iniciou || concluiu) return null;
                    iniciou = true; trecho = r.Timestamp;
                    break;
                case TipoRegistroTempo.Retomada:
                    if (!iniciou || trecho.HasValue || concluiu) return null;
                    trecho = r.Timestamp;
                    break;
                case TipoRegistroTempo.Pausa:
                case TipoRegistroTempo.Conclusao:
                    if (!trecho.HasValue || concluiu) return null;
                    total += (r.Timestamp - trecho.Value).TotalMinutes;
                    trecho = null; concluiu = r.TipoRegistro == TipoRegistroTempo.Conclusao;
                    break;
                default: return null;
            }
        }
        return iniciou && concluiu ? total : null;
    }
}
