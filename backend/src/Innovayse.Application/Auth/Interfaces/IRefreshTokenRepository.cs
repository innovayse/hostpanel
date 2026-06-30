namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Persistence contract for refresh token lifecycle operations.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Persists a new refresh token for a user.
    /// </summary>
    /// <param name="userId">The user who owns this token.</param>
    /// <param name="token">The cryptographically random token string.</param>
    /// <param name="expiresAt">UTC expiry of the token.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(string userId, string token, DateTimeOffset expiresAt, CancellationToken ct);

    /// <summary>
    /// Finds a refresh token by its raw string value.
    /// Returns <see langword="null"/> if not found.
    /// </summary>
    /// <param name="token">The raw refresh token string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (UserId, ExpiresAt, IsRevoked) or null.</returns>
    Task<(string UserId, DateTimeOffset ExpiresAt, bool IsRevoked)?> FindAsync(string token, CancellationToken ct);

    /// <summary>
    /// Marks a refresh token as revoked (used on logout or rotation).
    /// </summary>
    /// <param name="token">The raw refresh token string to revoke.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RevokeAsync(string token, CancellationToken ct);

    /// <summary>
    /// Revokes all active refresh tokens for a user (logout all devices).
    /// </summary>
    /// <param name="userId">The user whose tokens to revoke.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RevokeAllForUserAsync(string userId, CancellationToken ct);
}
