using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

public class PlanoPreventivoDto
{
    public Guid Id { get; set; }

    public Guid EquipamentoId { get; set; }
    public string EquipamentoNome { get; set; } = string.Empty;

    public Guid TecnicoResponsavelId { get; set; }
    public string TecnicoResponsavelNome { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;
    public int PeriodicidadeDias { get; set; }
    public DateOnly ProximaExecucao { get; set; }
    public bool Ativo { get; set; }
}

public class SalvarPlanoPreventivoDto
{
    [Required(ErrorMessage = "Informe o equipamento.")]
    public Guid EquipamentoId { get; set; }

    [Required(ErrorMessage = "Informe o técnico responsável.")]
    public Guid TecnicoResponsavelId { get; set; }

    [Required(ErrorMessage = "Informe a tarefa.")]
    [StringLength(300)]
    public string Descricao { get; set; } = string.Empty;

    [Range(1, 3650, ErrorMessage = "A periodicidade deve ser de 1 a 3650 dias.")]
    public int PeriodicidadeDias { get; set; }

    // Data da primeira (ou próxima) execução. Ao criar, costuma ser "hoje" ou uma data futura.
    [Required(ErrorMessage = "Informe a próxima execução.")]
    public DateOnly ProximaExecucao { get; set; }
}