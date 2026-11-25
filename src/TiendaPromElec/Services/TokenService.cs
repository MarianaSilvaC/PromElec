using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProductApi.Models;
using TiendaPromElec.Dtos;
using TiendaPromElec.Security;

namespace TiendaPromElec.Services;

/// <summary>Genera tokens JWT firmados.</summary>
public interface ITokenService
{
    AuthResponseDto CreateToken(ApplicationUser user, string role);
}

/// <summary>
/// H-01: el token lleva el id del usuario, su correo, su rol y (si es Cliente) su id de cliente.
/// Se firma con HMAC-SHA256 usando la llave secreta Jwt:Key y vence en Jwt:ExpiresMinutes.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _time;

    public TokenService(IOptions<JwtSettings> settings, TimeProvider time)
    {
        _settings = settings.Value;
        _time = time;
    }

    public AuthResponseDto CreateToken(ApplicationUser user, string role)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_settings.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id),
            new(ClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimNames.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.CustomerId is long customerId)
        {
            claims.Add(new Claim(ClaimNames.CustomerId, customerId.ToString(CultureInfo.InvariantCulture)));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AuthResponseDto(token, expires, user.Email ?? string.Empty, role);
    }
}
