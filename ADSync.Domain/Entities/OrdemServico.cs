using ADSync.Domain.Enums;

namespace ADSync.Domain.Entities;

public class OrdemServico
{
    public Guid Id { get; set; }

    // Gerado no PWA/IndexedDB enquanto offline; usado para evitar O.S. duplicada na sincronização.
    public Guid? ClientSyncId { get; set; }

    public string NumeroOS { get; set; } = string.Empty;

    public Guid EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    public Guid OperadorId { get; set; }
    public Usuario? Operador { get; set; }

    public Guid? TecnicoId { get; set; }
    public Usuario? Tecnico { get; set; }

    public Guid? CausaFalhaId { get; set; }
    public CausaFalha? CausaFalha { get; set; }

    public string DescricaoProblema { get; set; } = string.Empty;
    public StatusOS Status { get; set; } = StatusOS.Aberta;
    public PrioridadeOS Prioridade { get; set; } = PrioridadeOS.Media;

    public DateTimeOffset DataAbertura { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DataConclusao { get; set; }
    public string? ObservacoesTecnicas { get; set; }
    public bool CriadoOffline { get; set; }

    public ICollection<OsRegistroTempo> RegistrosTempo { get; set; } = new List<OsRegistroTempo>();
    public ICollection<OsPecaUtilizada> PecasUtilizadas { get; set; } = new List<OsPecaUtilizada>();
}
