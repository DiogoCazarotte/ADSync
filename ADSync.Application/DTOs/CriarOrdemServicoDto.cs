using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

// O que o app do operador/gestor envia para abrir uma O.S.
// (o operador NÃO vem aqui: a API usa o usuário do token)
public class CriarOrdemServicoDto
{
    // Preenchido pelo app quando a O.S. foi criada offline;
    // usado pela API para não duplicar o registro na sincronização.
    public Guid? ClientSyncId { get; set; }

    [Required(ErrorMessage = "Informe o equipamento.")]
    public Guid EquipamentoId { get; set; }

    // Preenchido pela API a partir do token. Qualquer valor enviado aqui é ignorado.
    public Guid OperadorId { get; set; }

    [Required(ErrorMessage = "Descreva o problema.")]
    [StringLength(2000)]
    public string DescricaoProblema { get; set; } = string.Empty;

    // "Baixa", "Media", "Alta" ou "Critica" — default é "Media" se vier vazio/invalido
    public string Prioridade { get; set; } = "Media";

    public bool CriadoOffline { get; set; }

    // Quando a O.S. foi realmente aberta (usado se ela nasceu offline). Se omitido, vale "agora".
    public DateTimeOffset? DataAbertura { get; set; }
}