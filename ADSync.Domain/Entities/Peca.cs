namespace ADSync.Domain.Entities;

public class Peca
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeEstoque { get; set; }

    public ICollection<OsPecaUtilizada> PecasUtilizadas { get; set; } = new List<OsPecaUtilizada>();
}
