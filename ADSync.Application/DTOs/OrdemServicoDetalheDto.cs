namespace ADSync.Application.DTOs;

// Versão completa: usada na tela de detalhe e como resposta das ações do fluxo.
public class OrdemServicoDetalheDto
{
    public Guid Id { get; set; }
    public string NumeroOS { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;

    public Guid EquipamentoId { get; set; }
    public string EquipamentoNome { get; set; } = string.Empty;
    public string Setor { get; set; } = string.Empty;

    public string OperadorNome { get; set; } = string.Empty;
    public Guid? TecnicoId { get; set; }
    public string? TecnicoNome { get; set; }

    public string DescricaoProblema { get; set; } = string.Empty;
    public string? CausaFalha { get; set; }
    public string? ObservacoesTecnicas { get; set; }

    public DateTimeOffset DataAbertura { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }
    public bool CriadoOffline { get; set; }

    // Soma só dos trechos efetivamente trabalhados (não conta as pausas)
    public double TempoAtendimentoMinutos { get; set; }

    public List<RegistroTempoDto> Registros { get; set; } = new();
    public List<PecaUtilizadaDto> Pecas { get; set; } = new();
}

public class RegistroTempoDto
{
    public string Tipo { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? MotivoPausa { get; set; }
}

public class PecaUtilizadaDto
{
    public Guid PecaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
}