namespace ADSync.Domain.Entities;

public class Equipamento
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CodigoPatrimonio { get; set; } = string.Empty;
    public string Setor { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public ICollection<OrdemServico> OrdensServico { get; set; } = new List<OrdemServico>();
}
