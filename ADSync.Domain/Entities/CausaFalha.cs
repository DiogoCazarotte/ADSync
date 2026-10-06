namespace ADSync.Domain.Entities;

public class CausaFalha
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public ICollection<OrdemServico> OrdensServico { get; set; } = new List<OrdemServico>();
}
