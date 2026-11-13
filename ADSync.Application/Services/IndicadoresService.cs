using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

public class IndicadoresService(IIndicadoresRepository repo) : IIndicadoresService
{
    public async Task<IndicadoresDto> ObterAsync(DateOnly dataInicio, DateOnly dataFim, string? setor)
    {
        if (dataInicio > dataFim || dataFim == DateOnly.MaxValue)
            throw new ArgumentException("Informe um período válido, com data final até 30/12/9999.");

        var dias = dataFim.DayNumber - dataInicio.DayNumber + 1;
        var inicio = Utc(dataInicio);
        var fimExclusivo = Utc(dataFim.AddDays(1));
        // O mesmo critério de conclusão e o mesmo setor valem para os dois períodos.
        var atuais = await repo.ListarConcluidasAsync(inicio, fimExclusivo);
        DateOnly? inicioAnterior = dataInicio.DayNumber >= dias ? dataInicio.AddDays(-dias) : null;
        var anteriores = inicioAnterior.HasValue
            ? await repo.ListarConcluidasAsync(Utc(inicioAnterior.Value), inicio)
            : new List<OrdemServico>();
        var equipamentos = repo.MtbfHabilitado ? await repo.ListarEquipamentosMtbfAsync() : [];
        var operacoes = repo.MtbfHabilitado ? await repo.ListarOperacoesAsync(dataInicio, dataFim) : [];
        var falhas = repo.MtbfHabilitado ? await repo.ListarFalhasAsync(inicio, fimExclusivo) : [];
        var operacoesAnteriores = repo.MtbfHabilitado && inicioAnterior.HasValue
            ? await repo.ListarOperacoesAsync(inicioAnterior.Value, dataInicio.AddDays(-1)) : [];
        var falhasAnteriores = repo.MtbfHabilitado && inicioAnterior.HasValue
            ? await repo.ListarFalhasAsync(Utc(inicioAnterior.Value), inicio) : [];
        var setores = atuais.Concat(anteriores).Select(SetorDe).Concat(equipamentos.Select(SetorEquipamento))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        var filtro = string.IsNullOrWhiteSpace(setor) ? null : setor.Trim();
        if (filtro is not null)
        {
            atuais = atuais.Where(os => string.Equals(SetorDe(os), filtro, StringComparison.OrdinalIgnoreCase)).ToList();
            anteriores = anteriores.Where(os => string.Equals(SetorDe(os), filtro, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var porSetor = atuais.GroupBy(SetorDe, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key).Select(g =>
        {
            var resumo = CalcularResumo(g.ToList());
            return new IndicadorPorSetorDto
            {
                Setor = g.Key,
                TotalConcluidas = resumo.TotalConcluidas,
                TotalPreventivas = resumo.TotalPreventivas,
                TotalCorretivas = resumo.TotalCorretivas,
                PercentualPreventiva = resumo.PercentualPreventiva,
                MttrMinutos = resumo.MttrMinutos,
                AmostraMttr = resumo.AmostraMttr,
                CorretivasSemTempo = resumo.CorretivasSemTempo,
                MtbfDias = resumo.MtbfDias,
                AmostraIntervalos = resumo.AmostraIntervalos
            };
        }).ToList();

        var mensal = dias > 31;
        DateOnly Chave(DateOnly data) => mensal ? new DateOnly(data.Year, data.Month, 1) : data;
        var contagens = atuais.GroupBy(os => Chave(DateOnly.FromDateTime(os.DataConclusao!.Value.UtcDateTime)))
            .ToDictionary(g => g.Key, g => new IndicadorEvolucaoDto
            {
                Data = g.Key,
                Preventivas = g.Count(os => os.TipoManutencao == TipoManutencao.Preventiva),
                Corretivas = g.Count(os => os.TipoManutencao == TipoManutencao.Corretiva)
            });
        var evolucao = new List<IndicadorEvolucaoDto>();
        for (var data = Chave(dataInicio); data <= dataFim;)
        {
            evolucao.Add(contagens.GetValueOrDefault(data) ?? new IndicadorEvolucaoDto { Data = data });
            if (mensal ? data.Year == dataFim.Year && data.Month == dataFim.Month : data == dataFim) break;
            data = mensal ? data.AddMonths(1) : data.AddDays(1);
        }

        var geral = CalcularResumo(atuais);
        var anterior = inicioAnterior.HasValue ? CalcularResumo(anteriores) : null;
        if (repo.MtbfHabilitado)
        {
            AplicarMtbf(geral, equipamentos, operacoes, falhas, dias, filtro);
            if (anterior is not null) AplicarMtbf(anterior, equipamentos, operacoesAnteriores, falhasAnteriores, dias, filtro);
            foreach (var nome in setores.Where(s => filtro is null || string.Equals(s, filtro, StringComparison.OrdinalIgnoreCase)))
            {
                var resumoSetor = porSetor.FirstOrDefault(s => string.Equals(s.Setor, nome, StringComparison.OrdinalIgnoreCase));
                if (resumoSetor is null) { resumoSetor = new IndicadorPorSetorDto { Setor = nome }; porSetor.Add(resumoSetor); }
                AplicarMtbf(resumoSetor, equipamentos, operacoes, falhas, dias, nome);
            }
        }

        return new IndicadoresDto
        {
            MtbfHabilitado = repo.MtbfHabilitado,
            DataInicio = dataInicio, DataFim = dataFim, Setor = filtro,
            DataInicioAnterior = inicioAnterior,
            DataFimAnterior = inicioAnterior.HasValue ? dataInicio.AddDays(-1) : null,
            Anterior = anterior,
            SetoresDisponiveis = setores,
            Geral = geral, PorSetor = porSetor,
            EvolucaoMensal = mensal, Evolucao = evolucao,
            Equipamentos = atuais.Where(os => os.TipoManutencao == TipoManutencao.Corretiva)
                .GroupBy(os => os.EquipamentoId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.First().Equipamento?.Nome).ThenBy(g => g.Key)
                .Take(5).Select(g => new IndicadorEquipamentoDto
                {
                    EquipamentoId = g.Key, Nome = g.First().Equipamento?.Nome ?? "Equipamento sem cadastro",
                    Setor = SetorDe(g.First()), Corretivas = g.Count(), MttrMinutos = CalcularResumo(g.ToList()).MttrMinutos
                }).ToList()
        };
    }

    private static DateTimeOffset Utc(DateOnly data) => new(data.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    private static string SetorEquipamento(Equipamento equipamento) => string.IsNullOrWhiteSpace(equipamento.Setor) ? "(sem setor)" : equipamento.Setor.Trim();

    private static void AplicarMtbf(IndicadorResumoDto resumo, List<Equipamento> equipamentos,
        List<OperacaoDiaria> operacoes, List<OrdemServico> falhas, int dias, string? setor)
    {
        // Um equipamento só entra quando todos os dias estão registrados, inclusive dias com zero horas.
        // Numerador e denominador sempre usam os mesmos equipamentos e a mesma janela.
        var horasPorEquipamento = operacoes.GroupBy(o => o.EquipamentoId).ToDictionary(g => g.Key, g => g.ToList());
        var idsComHistorico = operacoes.Select(o => o.EquipamentoId).Concat(falhas.Select(o => o.EquipamentoId)).ToHashSet();
        var candidatos = equipamentos.Where(e => (e.Ativo || idsComHistorico.Contains(e.Id)) &&
            (setor is null || string.Equals(SetorEquipamento(e), setor, StringComparison.OrdinalIgnoreCase))).ToList();
        var cobertos = candidatos.Where(e => horasPorEquipamento.TryGetValue(e.Id, out var registros)
            && registros.Select(o => o.Data).Distinct().Count() == dias).Select(e => e.Id).ToHashSet();
        resumo.EquipamentosMtbf = cobertos.Count;
        resumo.EquipamentosSemCobertura = candidatos.Count - cobertos.Count;
        resumo.HorasOperadas = operacoes.Where(o => cobertos.Contains(o.EquipamentoId)).Sum(o => o.HorasOperadas);
        resumo.FalhasMtbf = falhas.Count(o => cobertos.Contains(o.EquipamentoId));
        resumo.MtbfHoras = resumo.HorasOperadas > 0 && resumo.FalhasMtbf > 0
            ? Math.Round((double)resumo.HorasOperadas / resumo.FalhasMtbf, 1) : null;
    }
    private static string SetorDe(OrdemServico os) => string.IsNullOrWhiteSpace(os.Equipamento?.Setor) ? "(sem setor)" : os.Equipamento.Setor.Trim();

    private static IndicadorResumoDto CalcularResumo(List<OrdemServico> lista)
    {
        var preventivas = lista.Count(os => os.TipoManutencao == TipoManutencao.Preventiva);
        var corretivas = lista.Where(os => os.TipoManutencao == TipoManutencao.Corretiva).ToList();
        // Ausência de marcações não representa um reparo instantâneo. Mantém zero
        // quando há início e conclusão válidos no mesmo instante.
        var tempos = corretivas.Select(TempoValido).Where(t => t.HasValue).Select(t => t!.Value).ToList();
        var intervalosDias = new List<double>();
        foreach (var grupo in corretivas.GroupBy(os => os.EquipamentoId))
        {
            var aberturas = grupo.Select(os => os.DataAbertura).OrderBy(d => d).ToList();
            for (var i = 1; i < aberturas.Count; i++)
                intervalosDias.Add((aberturas[i] - aberturas[i - 1]).TotalDays);
        }
        return new IndicadorResumoDto
        {
            TotalConcluidas = lista.Count, TotalPreventivas = preventivas, TotalCorretivas = corretivas.Count,
            PercentualPreventiva = lista.Count > 0 ? Math.Round(preventivas * 100.0 / lista.Count, 1) : null,
            MttrMinutos = tempos.Count > 0 ? Math.Round(tempos.Average(), 1) : null,
            AmostraMttr = tempos.Count, CorretivasSemTempo = corretivas.Count - tempos.Count,
            MtbfDias = intervalosDias.Count > 0 ? Math.Round(intervalosDias.Average(), 1) : null,
            AmostraIntervalos = intervalosDias.Count
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
                    iniciou = true;
                    trecho = r.Timestamp;
                    break;
                case TipoRegistroTempo.Retomada:
                    if (!iniciou || trecho.HasValue || concluiu) return null;
                    trecho = r.Timestamp;
                    break;
                case TipoRegistroTempo.Pausa:
                case TipoRegistroTempo.Conclusao:
                    if (!trecho.HasValue || concluiu) return null;
                    total += (r.Timestamp - trecho.Value).TotalMinutes;
                    trecho = null;
                    concluiu = r.TipoRegistro == TipoRegistroTempo.Conclusao;
                    break;
            }
        }
        return iniciou && concluiu ? total : null;
    }
}
