using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ProjectA.Api.Security;

// Single source of truth for the three Jwt:* settings, shared between token issuance here
// (used by the login endpoint) and the AddJwtBearer validation setup in Program.cs, which
// reads the same configuration keys directly since it has to run before DI is available.
public sealed class JwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _signingKey;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        _signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        _expiryMinutes = configuration.GetValue("Jwt:ExpiryMinutes", 720);
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(long userId, string username)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_expiryMinutes);

        Claim[] claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("uid", userId.ToString())
        ];

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
