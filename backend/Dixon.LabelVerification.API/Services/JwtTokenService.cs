using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dixon.LabelVerification.API.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Dixon.LabelVerification.API.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(User user)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer must be configured.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience must be configured.");
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey must be configured through a secret store or environment.");
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(60);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim("preferred_username", user.EmployeeId),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        if (user.BrandId.HasValue)
            claims.Add(new Claim("brand_id", user.BrandId.Value.ToString()));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer, audience, claims, DateTime.UtcNow, expiresAt.UtcDateTime, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
