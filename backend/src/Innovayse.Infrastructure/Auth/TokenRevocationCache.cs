namespace Innovayse.Infrastructure.Auth;

using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// In-memory cache that tracks recently revoked user sessions.
/// When a password is changed, the user ID is stored with the revocation time.
/// JWT middleware checks this cache to reject tokens issued before the revocation.
/// Entries auto-expire after the access token lifetime (15 minutes).
/// </summary>
public sealed class TokenRevocationCache
{
    /// <summary>Cache key prefix for revoked user entries.</summary>
    private const string KeyPrefix = "revoked:";

    /// <summary>
    /// Entries expire after this duration, matching the JWT access token lifetime.
    /// After expiry, old tokens have naturally expired anyway.
    /// </summary>
    private static readonly TimeSpan _entryLifetime = TimeSpan.FromMinutes(16);

    /// <summary>Underlying memory cache instance.</summary>
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRevocationCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache provided by DI.</param>
    public TokenRevocationCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Marks all tokens issued before now as revoked for the given user.
    /// </summary>
    /// <param name="userId">The user whose tokens should be invalidated.</param>
    public void RevokeUser(string userId)
    {
        _cache.Set($"{KeyPrefix}{userId}", DateTimeOffset.UtcNow, _entryLifetime);
    }

    /// <summary>
    /// Checks whether a token issued at the given time has been revoked for the user.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    /// <param name="tokenIssuedAt">The <c>iat</c> claim from the JWT.</param>
    /// <returns>True if the token was issued before the revocation time.</returns>
    public bool IsRevoked(string userId, DateTimeOffset tokenIssuedAt)
    {
        if (_cache.TryGetValue<DateTimeOffset>($"{KeyPrefix}{userId}", out var revokedAt))
        {
            return tokenIssuedAt < revokedAt;
        }

        return false;
    }
}
