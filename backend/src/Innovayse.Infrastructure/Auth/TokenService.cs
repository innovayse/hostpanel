namespace Innovayse.Infrastructure.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Generates signed JWT access tokens and cryptographically random refresh tokens.
/// Implements <see cref="ITokenService"/>.
/// </summary>
/// <param name="configuration">App configuration — reads <c>Jwt:Secret</c>, <c>Jwt:Issuer</c>, <c>Jwt:Audience</c>.</param>
/// <param name="db">DbContext used for refresh token validation queries.</param>
public sealed class TokenService(IConfiguration configuration, AppDbContext db) : ITokenService
{
    /// <summary>JWT access token lifetime.</summary>
    private static readonly TimeSpan _accessTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Refresh token lifetime.</summary>
    private static readonly TimeSpan _refreshTokenLifetime = TimeSpan.FromDays(7);

    /// <inheritdoc/>
    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(string userId, string email, string role)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTimeOffset.UtcNow.Add(_accessTokenLifetime);

        var now = DateTimeOffset.UtcNow;

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        ];

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <inheritdoc/>
    public (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken(string userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);
        return (token, DateTimeOffset.UtcNow.Add(_refreshTokenLifetime));
    }

    /// <inheritdoc/>
    public async Task<string?> ValidateRefreshTokenAsync(string token, CancellationToken ct)
    {
        var record = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (record is null || record.IsRevoked || record.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return null;
        }

        return record.UserId;
    }
}
