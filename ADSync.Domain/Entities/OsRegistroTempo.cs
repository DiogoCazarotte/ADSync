using ADSync.Domain.Enums;

namespace ADSync.Domain.Entities;

public class OsRegistroTempo
{
    public Guid Id { get; set; }

    public Guid OrdemServicoId { get; set; }
    public OrdemServico? OrdemServico { get; set; }

    public TipoRegistroTempo TipoRegistro { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? MotivoPausa { get; set; }
}
