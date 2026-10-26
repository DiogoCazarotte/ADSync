namespace ADSync.Application.DTOs;

// Versão resumida: usada nas listagens e ao abrir uma O.S.
public class OrdemServicoDto
{
    public Guid Id { get; set; }
    public string NumeroOS { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public string TipoManutencao { get; set; } = string.Empty;
    public bool GeradaAutomaticamente { get; set; }
    public string EquipamentoNome { get; set; } = string.Empty;
    public string OperadorNome { get; set; } = string.Empty;
    public string? TecnicoNome { get; set; }
    public string DescricaoProblema { get; set; } = string.Empty;
    public DateTimeOffset DataAbertura { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }
}