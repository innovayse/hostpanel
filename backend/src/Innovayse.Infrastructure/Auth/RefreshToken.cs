namespace Innovayse.Infrastructure.Auth;

/// <summary>
/// Persisted refresh token issued to a user after login or token rotation.
/// Stored in the <c>refresh_tokens</c> table.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Gets or sets the primary key (auto-increment).</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the foreign key linking to the <see cref="AppUser"/> primary key.</summary>
    public required string UserId { get; set; }

    /// <summary>Gets or sets the cryptographically random opaque token string.</summary>
    public required string Token { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this token expires (default 7 days).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this token was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets a value indicating whether this token has been revoked.</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Gets or sets the navigation property to the owning user.</summary>
    public AppUser? User { get; set; }
}
