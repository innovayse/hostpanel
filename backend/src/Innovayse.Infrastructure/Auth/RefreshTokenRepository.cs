namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenRepository"/>.
/// Operates on the <c>refresh_tokens</c> table via <see cref="AppDbContext"/>.
/// </summary>
/// <param name="db">The application DbContext.</param>
public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    /// <inheritdoc/>
    public Task AddAsync(string userId, string token, DateTimeOffset expiresAt, CancellationToken ct)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<(string UserId, DateTimeOffset ExpiresAt, bool IsRevoked)?> FindAsync(
        string token, CancellationToken ct)
    {
        var record = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (record is null)
        {
            return null;
        }

        return (record.UserId, record.ExpiresAt, record.IsRevoked);
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        var record = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (record is not null)
        {
            record.IsRevoked = true;
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct);
    }
}
