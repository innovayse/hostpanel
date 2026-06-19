namespace Innovayse.Infrastructure.Auth;

using System.Collections.Concurrent;
using Innovayse.Application.Auth.Interfaces;

/// <summary>
/// In-memory store for pre-authentication tokens issued when 2FA is required.
/// Maps a random URL-safe token to a userId with a 5-minute expiry.
/// Registered as a singleton so state survives across requests within the process lifetime.
/// </summary>
public sealed class TwoFactorPendingCache : ITwoFactorPendingCache
{
    private readonly ConcurrentDictionary<string, (string UserId, DateTimeOffset ExpiresAt)> _store = new();

    /// <inheritdoc/>
    public string CreatePendingToken(string userId)
    {
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        _store[token] = (userId, DateTimeOffset.UtcNow.AddMinutes(5));
        return token;
    }

    /// <inheritdoc/>
    public string? ConsumePendingToken(string token)
    {
        if (_store.TryRemove(token, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return entry.UserId;
        }

        return null;
    }
}
