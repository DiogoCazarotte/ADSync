namespace ADSync.Application.DTOs;

// O que o app do técnico/operador envia para abrir uma O.S.
public class CriarOrdemServicoDto
{
    // Preenchido pelo app quando a O.S. foi criada offline;
    // usado pela API para não duplicar o registro na sincronização.
    public Guid? ClientSyncId { get; set; }

    public Guid EquipamentoId { get; set; }
    public Guid OperadorId { get; set; }
    public string DescricaoProblema { get; set; } = string.Empty;

   
    public string Prioridade { get; set; } = "Media";

    public bool CriadoOffline { get; set; }
}
