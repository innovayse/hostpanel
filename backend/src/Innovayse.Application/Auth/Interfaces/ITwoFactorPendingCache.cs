namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// In-memory store for pre-authentication tokens issued when 2FA is required.
/// Maps a random token to a userId with a short expiry window.
/// </summary>
public interface ITwoFactorPendingCache
{
    /// <summary>
    /// Creates a short-lived pre-auth token for the given user and stores it.
    /// </summary>
    /// <param name="userId">The ID of the user who passed credentials but still needs to verify a TOTP code.</param>
    /// <returns>A URL-safe random token string.</returns>
    string CreatePendingToken(string userId);

    /// <summary>
    /// Atomically removes and returns the userId bound to the given token.
    /// Returns null if the token does not exist or has expired.
    /// </summary>
    /// <param name="token">The pre-auth token to consume.</param>
    /// <returns>The bound userId, or null if invalid/expired.</returns>
    string? ConsumePendingToken(string token);
}
