namespace ADSync.Domain.Entities;

public class OperacaoDiaria
{
    public Guid EquipamentoId { get; set; }
    public Equipamento Equipamento { get; set; } = null!;
    public DateOnly Data { get; set; }
    public decimal HorasOperadas { get; set; }
}
