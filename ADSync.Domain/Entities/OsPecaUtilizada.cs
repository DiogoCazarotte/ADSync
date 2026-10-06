namespace ADSync.Domain.Entities;

public class OsPecaUtilizada
{
    public Guid Id { get; set; }

    public Guid OrdemServicoId { get; set; }
    public OrdemServico? OrdemServico { get; set; }

    public Guid PecaId { get; set; }
    public Peca? Peca { get; set; }

    public int Quantidade { get; set; }
}
