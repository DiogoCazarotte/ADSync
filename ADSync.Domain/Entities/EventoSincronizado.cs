namespace ADSync.Domain.Entities;

public class EventoSincronizado
{
    public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTimeOffset OcorridoEm { get; set; }
    public string Hash { get; set; } = "";
    public string Resposta { get; set; } = "";
}
