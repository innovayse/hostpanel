namespace Innovayse.Infrastructure.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Innovayse.Application.Auth.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Issues local JWTs when Auth:Mode=local (no SSO dependency).
/// </summary>
/// <remarks>
/// Lives in Infrastructure rather than beside the controller that used to hold it: signing a
/// token is I/O-adjacent work behind the <see cref="IJwtService"/> port, and an implementation
/// parked in the API project is a dependency pointing the wrong way -- nothing below the web
/// edge could reach it, and the controller injected the concrete class instead of a contract.
/// </remarks>
/// <param name="config">
/// Application configuration. Read here rather than through an options class because the three
/// keys below are shared verbatim with the token *validators* built in the composition root, and
/// splitting the two readers is how a signer and a validator end up disagreeing about a key.
/// </param>
public sealed class JwtTokenService(IConfiguration config) : IJwtService
{
    /// <summary>
    /// Dev-only fallback signing secret, used when Jwt:Secret is unset. Program.cs applies
    /// this same fallback for validating incoming tokens -- shared here as the one place both
    /// sides read from, so a token this service signs is never rejected by the validator that's
    /// supposed to accept it (or, unset, throws instead of silently signing with two different
    /// keys). Never set in a deployed environment; validated there via Program.cs's length check.
    /// </summary>
    public const string DevSecretFallback = "change-this-to-a-32-char-min-secret-key-here";

    /// <summary>
    /// Default issuer/audience, used whenever Jwt:Issuer / Jwt:Audience are unset. Unlike
    /// <see cref="DevSecretFallback"/> these are fine to run with in any environment -- they're
    /// just identifier strings, not a secret -- but they still have to be the exact values
    /// Program.cs's token validators fall back to, or a token minted without either config key
    /// set carries issuer/audience: null and gets rejected by validators expecting these
    /// defaults, which is a 401 on every request with an otherwise-valid token.
    /// </summary>
    public const string DefaultIssuer = "innovayse-api";

    /// <summary>See <see cref="DefaultIssuer"/>.</summary>
    public const string DefaultAudience = "innovayse-clients";

    /// <summary>How long an issued access token stays valid.</summary>
    /// <remarks>
    /// Deliberately short: there is no refresh store yet, so a leaked token has to expire on its
    /// own. The client portal reports the same number to its caller as <c>expiresIn</c> seconds.
    /// </remarks>
    private const int AccessTokenLifetimeMinutes = 15;

    /// <summary>Number of random bytes behind a refresh token, before base64 encoding.</summary>
    private const int RefreshTokenBytes = 32;

    /// <inheritdoc/>
    public string GenerateAccessToken(
        string userId,
        string email,
        string? firstName,
        string? lastName,
        IReadOnlyList<string> roles,
        bool emailVerified = true)
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
            expires: DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
    }
}
