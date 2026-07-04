namespace Innovayse.API.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>Issues local JWTs when Auth:Mode=local (no SSO dependency).</summary>
public sealed class JwtTokenService(IConfiguration config)
{
    /// <summary>Generates a short-lived (15 min) access token for the given user.</summary>
    public string GenerateAccessToken(string userId, string email, string? firstName, string? lastName, IList<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("email", email),
        };
        if (!string.IsNullOrEmpty(firstName))
            claims.Add(new("given_name", firstName));
        if (!string.IsNullOrEmpty(lastName))
            claims.Add(new("family_name", lastName));
        foreach (var role in roles)
            claims.Add(new(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Generates a cryptographically random refresh token (base64, 32 bytes).</summary>
    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}
