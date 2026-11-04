namespace ADSync.Application.DTOs;

// Resumo de indicadores de um grupo de O.S. (geral ou de um setor).
public class IndicadorResumoDto
{
    public int TotalConcluidas { get; set; }
    public int TotalPreventivas { get; set; }
    public int TotalCorretivas { get; set; }

    // % de manutenção preventiva no total de O.S. concluídas. Null se não houve nenhuma no período.
    public double? PercentualPreventiva { get; set; }

    // MTTR: tempo médio de reparo, em minutos. Calculado só sobre as CORRETIVAS
    // (é o tempo médio para consertar uma falha). Null se não houve corretiva concluída.
    public double? MttrMinutos { get; set; }

    // MTBF: tempo médio entre falhas, em dias. Calculado só sobre as CORRETIVAS, por
    // equipamento (diferença entre as aberturas de falhas seguidas do mesmo equipamento),
    // e depois a média de todos esses intervalos. Null se não há pelo menos 2 corretivas
    // do mesmo equipamento no período.
    public double? MtbfDias { get; set; }
}

public class IndicadorPorSetorDto : IndicadorResumoDto
{
    public string Setor { get; set; } = string.Empty;
}

public class IndicadoresDto
{
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }

    // Calculado sobre o mesmo conjunto filtrado (se "setor" foi informado na consulta,
    // Geral já reflete só aquele setor, e PorSetor terá uma única linha igual a Geral).
    public IndicadorResumoDto Geral { get; set; } = new();
    public List<IndicadorPorSetorDto> PorSetor { get; set; } = new();
}