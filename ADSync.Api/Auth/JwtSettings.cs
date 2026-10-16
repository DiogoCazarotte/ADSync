namespace ADSync.Api.Auth;

// Lida da seção "Jwt" da configuração.
// Issuer, Audience e ExpirationMinutes ficam no appsettings.json;
// a Key (segredo de assinatura) NUNCA vai no arquivo: fica no user secrets.
public class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ADSync.Api";
    public string Audience { get; set; } = "ADSync.Client";
    public int ExpirationMinutes { get; set; } = 480;
}