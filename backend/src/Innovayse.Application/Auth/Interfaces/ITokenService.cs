namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Generates and validates JWT access tokens and opaque refresh tokens.
/// Implemented in Infrastructure; never referenced directly from Domain.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT access token for the given user and role.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="email">The user's email address, embedded as a claim.</param>
    /// <param name="role">The user's primary role (Admin, Reseller, Client).</param>
    /// <returns>Tuple of the signed token string and its UTC expiry.</returns>
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(string userId, string email, string role);

    /// <summary>
    /// Creates a new cryptographically random refresh token string and its expiry.
    /// The caller (handler) passes these to the repository to persist.
    /// </summary>
    /// <param name="userId">The user the refresh token belongs to.</param>
    /// <returns>Tuple of the raw token string and its UTC expiry.</returns>
    (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken(string userId);

    /// <summary>
    /// Validates a refresh token string.
    /// Returns the associated user ID if valid, <see langword="null"/> if expired/revoked/not found.
    /// </summary>
    /// <param name="token">The raw refresh token string from the client cookie.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user ID associated with the token, or <see langword="null"/>.</returns>
    Task<string?> ValidateRefreshTokenAsync(string token, CancellationToken ct);
}
