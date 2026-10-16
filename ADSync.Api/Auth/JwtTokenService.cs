using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ADSync.Api.Auth;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiraEm) GerarToken(Guid userId, string email, string nome, IEnumerable<string> papeis);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public (string Token, DateTime ExpiraEm) GerarToken(Guid userId, string email, string nome, IEnumerable<string> papeis)
    {
        var expiraEm = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);

        // Claims com nomes curtos e padronizados ("sub", "email", "name", "role").
        // O Program.cs lê esses mesmos nomes (MapInboundClaims = false).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("name", nome),
        };
        claims.AddRange(papeis.Select(papel => new Claim("role", papel)));

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiraEm,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiraEm);
    }
}