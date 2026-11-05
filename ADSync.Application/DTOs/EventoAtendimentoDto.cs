using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

public class EventoAtendimentoDto
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    [Required] public string Acao { get; set; } = "";
    public DateTimeOffset OcorridoEm { get; set; }
    [StringLength(255)] public string? Motivo { get; set; }
    public Guid? PecaId { get; set; }
    [Range(0, 10000)] public int Quantidade { get; set; }
    public Guid? CausaFalhaId { get; set; }
    [StringLength(2000)] public string? Observacoes { get; set; }
}
