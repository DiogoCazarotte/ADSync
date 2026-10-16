using System.ComponentModel.DataAnnotations;

namespace ADSync.Application.DTOs;

// O que o app envia para fazer login
public class LoginDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Senha { get; set; } = string.Empty;
}

// O que a API devolve quando o login dá certo
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Papeis { get; set; } = new();
}