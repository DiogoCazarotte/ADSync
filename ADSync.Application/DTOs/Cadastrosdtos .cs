using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

// ---------- Equipamentos ----------
public class EquipamentoDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CodigoPatrimonio { get; set; } = string.Empty;
    public string Setor { get; set; } = string.Empty;
    public bool Ativo { get; set; }
}

public class SalvarEquipamentoDto
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o código de patrimônio.")]
    [StringLength(50)]
    public string CodigoPatrimonio { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o setor.")]
    [StringLength(100)]
    public string Setor { get; set; } = string.Empty;
}

// ---------- Causas de falha ----------
public class CausaFalhaDto
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; }
}

public class SalvarCausaFalhaDto
{
    [Required(ErrorMessage = "Informe a descrição.")]
    [StringLength(150)]
    public string Descricao { get; set; } = string.Empty;
}

// ---------- Peças ----------
public class PecaDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeEstoque { get; set; }
    public bool Critica { get; set; }
}

public class CriarPecaDto
{
    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    [Range(0, 1000000, ErrorMessage = "A quantidade inicial deve ser zero ou mais.")]
    public int QuantidadeInicial { get; set; }

    // Marque quando a falta desta peça para o equipamento em funcionamento.
    public bool Critica { get; set; }
}

public class AtualizarPecaDto
{
    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150)]
    public string Nome { get; set; } = string.Empty;

    public bool Critica { get; set; }
}

public class EntradaEstoqueDto
{
    [Range(1, 1000000, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }
}

// ---------- Usuários (somente leitura por enquanto) ----------
public class UsuarioDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TipoUsuario { get; set; } = string.Empty;
    public bool Ativo { get; set; }
}