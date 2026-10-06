using ADSync.Domain.Enums;

namespace ADSync.Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public TipoUsuario TipoUsuario { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTimeOffset DataCriacao { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OrdemServico> OrdensComoOperador { get; set; } = new List<OrdemServico>();
    public ICollection<OrdemServico> OrdensComoTecnico { get; set; } = new List<OrdemServico>();
}
