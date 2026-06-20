namespace Innovayse.Domain.Ssl;

using Innovayse.Domain.Common;

/// <summary>Cached SSL certificate check result for a domain.</summary>
public sealed class SslCheck : Entity
{
    /// <summary>The domain name that was checked.</summary>
    public string DomainName { get; private set; } = string.Empty;

    /// <summary>Whether a valid SSL certificate was detected.</summary>
    public bool HasSsl { get; private set; }

    /// <summary>Certificate issuer (e.g. "Let's Encrypt"), null if no SSL.</summary>
    public string? Issuer { get; private set; }

    /// <summary>Certificate expiry date, null if no SSL.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>When this check was last performed.</summary>
    public DateTimeOffset CheckedAt { get; private set; }

    /// <summary>Whether this domain is currently active.</summary>
    public bool IsActive { get; private set; }

    private SslCheck() : base(0) { }

    /// <summary>Creates a new SSL check result for the given domain.</summary>
    public static SslCheck Create(string domainName, bool hasSSL, string? issuer, DateTimeOffset? expiresAt, bool isActive)
        => new()
        {
            DomainName = domainName,
            HasSsl = hasSSL,
            Issuer = issuer,
            ExpiresAt = expiresAt,
            CheckedAt = DateTimeOffset.UtcNow,
            IsActive = isActive,
        };

    /// <summary>Updates this SSL check with fresh results.</summary>
    public void Update(bool hasSSL, string? issuer, DateTimeOffset? expiresAt, bool isActive)
    {
        HasSsl = hasSSL;
        Issuer = issuer;
        ExpiresAt = expiresAt;
        CheckedAt = DateTimeOffset.UtcNow;
        IsActive = isActive;
    }
}
