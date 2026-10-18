using ADSync.Domain.Exceptions;

namespace ADSync.Domain.Entities;

public class Peca
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeEstoque { get; set; }

    public ICollection<OsPecaUtilizada> PecasUtilizadas { get; set; } = new List<OsPecaUtilizada>();

    // Regra: não dá para retirar mais do que existe em estoque.
    public void RetirarDoEstoque(int quantidade)
    {
        if (quantidade <= 0)
            throw new RegraNegocioException("A quantidade deve ser maior que zero.");

        if (quantidade > QuantidadeEstoque)
            throw new RegraNegocioException(
                $"Estoque insuficiente de '{Nome}'. Disponível: {QuantidadeEstoque}.");

        QuantidadeEstoque -= quantidade;
    }
}