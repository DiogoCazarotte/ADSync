namespace ADSync.Application.DTOs;

// O que a API devolve para o app (nunca a entidade do Domain diretamente)
public class OrdemServicoDto
{
    public Guid Id { get; set; }
    public string NumeroOS { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Prioridade { get; set; } = string.Empty;
    public string EquipamentoNome { get; set; } = string.Empty;
    public string? TecnicoNome { get; set; }
    public string DescricaoProblema { get; set; } = string.Empty;
    public DateTimeOffset DataAbertura { get; set; }
    public DateTimeOffset? DataConclusao { get; set; }
}
