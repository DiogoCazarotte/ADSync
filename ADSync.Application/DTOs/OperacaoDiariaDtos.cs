using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

public class SalvarOperacaoDiariaDto
{
    [Range(typeof(decimal), "0", "24", ErrorMessage = "Informe de 0 a 24 horas operadas.")]
    public decimal HorasOperadas { get; set; }
}

public class OperacaoDiariaDto
{
    public Guid EquipamentoId { get; set; }
    public string EquipamentoNome { get; set; } = "";
    public DateOnly Data { get; set; }
    public decimal HorasOperadas { get; set; }
}
