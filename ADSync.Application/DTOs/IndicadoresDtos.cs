namespace ADSync.Application.DTOs;

// Resumo de indicadores de um grupo de O.S. (geral ou de um setor).
public class IndicadorResumoDto
{
    public int TotalConcluidas { get; set; }
    public int TotalPreventivas { get; set; }
    public int TotalCorretivas { get; set; }

    // % de manutenção preventiva no total de O.S. concluídas. Null se não houve nenhuma no período.
    public double? PercentualPreventiva { get; set; }

    // Média de trabalho ativo nas corretivas concluídas com sequência válida de
    // início/retomada e pausa/conclusão. Null quando não há amostra válida.
    public double? MttrMinutos { get; set; }
    public int AmostraMttr { get; set; }
    public int CorretivasSemTempo { get; set; }

    // Nome preservado por compatibilidade com a API. É o intervalo entre aberturas
    // consecutivas das corretivas concluídas no período, por equipamento, em dias;
    // não usa horas reais de operação. Null quando não há ao menos um intervalo.
    public double? MtbfDias { get; set; }
    public int AmostraIntervalos { get; set; }
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
    public DateOnly? DataInicioAnterior { get; set; }
    public DateOnly? DataFimAnterior { get; set; }
    public IndicadorResumoDto? Anterior { get; set; }
    public string? Setor { get; set; }
    public List<string> SetoresDisponiveis { get; set; } = new();
    public bool EvolucaoMensal { get; set; }
    public List<IndicadorEvolucaoDto> Evolucao { get; set; } = new();
    public List<IndicadorEquipamentoDto> Equipamentos { get; set; } = new();
}

public class IndicadorEvolucaoDto
{
    public DateOnly Data { get; set; }
    public int Preventivas { get; set; }
    public int Corretivas { get; set; }
}

public class IndicadorEquipamentoDto
{
    public Guid EquipamentoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Setor { get; set; } = string.Empty;
    public int Corretivas { get; set; }
    public double? MttrMinutos { get; set; }
}
