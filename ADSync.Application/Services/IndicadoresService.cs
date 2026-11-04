using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

// RF07/RF10: MTTR, MTBF e % de manutenção preventiva — o "placar" da transição
// de corretivo para preventivo que a demanda da SustenNutri pede.
public class IndicadoresService : IIndicadoresService
{
    private readonly IIndicadoresRepository _repo;
    public IndicadoresService(IIndicadoresRepository repo) => _repo = repo;

    public async Task<IndicadoresDto> ObterAsync(DateOnly dataInicio, DateOnly dataFim, string? setor)
    {
        var inicio = new DateTimeOffset(dataInicio.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        // fim é inclusivo para quem escolhe a data (ex.: "até 31/10" inclui o dia inteiro),
        // então a consulta usa o dia seguinte como limite exclusivo.
        var fimExclusivo = new DateTimeOffset(dataFim.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var concluidas = await _repo.ListarConcluidasAsync(inicio, fimExclusivo);

        if (!string.IsNullOrWhiteSpace(setor))
        {
            var termo = setor.Trim();
            concluidas = concluidas
                .Where(os => string.Equals(os.Equipamento?.Setor, termo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var porSetor = concluidas
            .GroupBy(os => os.Equipamento?.Setor ?? "(sem setor)")
            .OrderBy(g => g.Key)
            .Select(g =>
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
                    MtbfDias = resumo.MtbfDias
                };
            })
            .ToList();

        return new IndicadoresDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            Geral = CalcularResumo(concluidas),
            PorSetor = porSetor
        };
    }

    private static IndicadorResumoDto CalcularResumo(List<OrdemServico> lista)
    {
        var total = lista.Count;
        var preventivas = lista.Count(os => os.TipoManutencao == TipoManutencao.Preventiva);
        var corretivas = lista.Where(os => os.TipoManutencao == TipoManutencao.Corretiva).ToList();

        double? mttr = corretivas.Count > 0
            ? Math.Round(corretivas.Average(os => os.CalcularTempoAtendimento(DateTimeOffset.UtcNow).TotalMinutes), 1)
            : null;

        var intervalosDias = new List<double>();
        foreach (var grupo in corretivas.GroupBy(os => os.EquipamentoId))
        {
            var aberturas = grupo.Select(os => os.DataAbertura).OrderBy(d => d).ToList();
            for (var i = 1; i < aberturas.Count; i++)
                intervalosDias.Add((aberturas[i] - aberturas[i - 1]).TotalDays);
        }
        double? mtbf = intervalosDias.Count > 0 ? Math.Round(intervalosDias.Average(), 1) : null;

        double? percentualPreventiva = total > 0 ? Math.Round(preventivas * 100.0 / total, 1) : null;

        return new IndicadorResumoDto
        {
            TotalConcluidas = total,
            TotalPreventivas = preventivas,
            TotalCorretivas = corretivas.Count,
            PercentualPreventiva = percentualPreventiva,
            MttrMinutos = mttr,
            MtbfDias = mtbf
        };
    }
}