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
