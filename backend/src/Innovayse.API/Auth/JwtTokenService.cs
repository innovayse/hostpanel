namespace Innovayse.API.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>Issues local JWTs when Auth:Mode=local (no SSO dependency).</summary>
public sealed class JwtTokenService(IConfiguration config)
{
    /// <summary>
    /// Dev-only fallback signing secret, used when Jwt:Secret is unset. Program.cs applies
    /// this same fallback for validating incoming tokens — shared here as the one place both
    /// sides read from, so a token this service signs is never rejected by the validator that's
    /// supposed to accept it (or, unset, throws instead of silently signing with two different
    /// keys). Never set in a deployed environment; validated there via Program.cs's length check.
    /// </summary>
    public const string DevSecretFallback = "change-this-to-a-32-char-min-secret-key-here";

    /// <summary>
    /// Default issuer/audience, used whenever Jwt:Issuer / Jwt:Audience are unset. Unlike
    /// <see cref="DevSecretFallback"/> these are fine to run with in any environment — they're
    /// just identifier strings, not a secret — but they still have to be the exact values
    /// Program.cs's token validators fall back to, or a token minted without either config key
    /// set carries issuer/audience: null and gets rejected by validators expecting these
    /// defaults, which is a 401 on every request with an otherwise-valid token.
    /// </summary>
    public const string DefaultIssuer = "innovayse-api";

    /// <summary>See <see cref="DefaultIssuer"/>.</summary>
    public const string DefaultAudience = "innovayse-clients";

    /// <summary>Generates a short-lived (15 min) access token for the given user.</summary>
    public string GenerateAccessToken(string userId, string email, string? firstName, string? lastName, IList<string> roles, bool emailVerified = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? DevSecretFallback));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("email", email),
            new("email_verified", emailVerified ? "true" : "false"),
        };
        if (!string.IsNullOrEmpty(firstName))
            claims.Add(new("given_name", firstName));
        if (!string.IsNullOrEmpty(lastName))
            claims.Add(new("family_name", lastName));
        foreach (var role in roles)
            claims.Add(new(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? DefaultIssuer,
            audience: config["Jwt:Audience"] ?? DefaultAudience,
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
