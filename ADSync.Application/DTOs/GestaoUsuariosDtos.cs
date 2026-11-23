using System.ComponentModel.DataAnnotations;
using ADSync.Domain.Enums;

namespace ADSync.Application.DTOs;

public class SalvarUsuarioDto
{
    [Required, StringLength(150)]
    public string Nome { get; set; } = "";
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";
    [EnumDataType(typeof(TipoUsuario))]
    public TipoUsuario TipoUsuario { get; set; }
}

public class CriarUsuarioDto : SalvarUsuarioDto
{
    [Required, StringLength(128, MinimumLength = 8)]
    public string Senha { get; set; } = "";
}

public class SituacaoUsuarioDto
{
    public bool Ativo { get; set; }
}

public class AlterarSenhaUsuarioDto
{
    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 128 caracteres.")]
    public string NovaSenha { get; set; } = "";

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem.")]
    public string ConfirmarSenha { get; set; } = "";
}
