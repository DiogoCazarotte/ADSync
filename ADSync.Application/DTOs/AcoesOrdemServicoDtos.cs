using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

// Corpo das ações do fluxo da O.S.

public class AtribuirTecnicoDto
{
    [Required(ErrorMessage = "Informe o técnico.")]
    public Guid? TecnicoId { get; set; }
}

// Usado em "iniciar" e "retomar". O corpo é opcional.
public class EventoTempoDto
{
    // Quando o evento realmente aconteceu (importante se o técnico estava offline).
    // Se omitido, vale "agora".
    public DateTimeOffset? OcorridoEm { get; set; }
}

public class PausarOrdemServicoDto
{
    [Required(ErrorMessage = "Informe o motivo da pausa.")]
    [StringLength(255)]
    public string Motivo { get; set; } = string.Empty;

    public DateTimeOffset? OcorridoEm { get; set; }
}

public class ConcluirOrdemServicoDto
{
    [Required(ErrorMessage = "Informe a causa da falha.")]
    public Guid? CausaFalhaId { get; set; }

    [StringLength(2000)]
    public string? ObservacoesTecnicas { get; set; }

    public DateTimeOffset? OcorridoEm { get; set; }
}

// O corpo é opcional.
public class CancelarOrdemServicoDto
{
    [StringLength(500)]
    public string? Motivo { get; set; }
}

public class RegistrarPecaDto
{
    [Required(ErrorMessage = "Informe a peça.")]
    public Guid? PecaId { get; set; }

    [Range(1, 10000, ErrorMessage = "A quantidade deve ser entre 1 e 10000.")]
    public int Quantidade { get; set; }
}