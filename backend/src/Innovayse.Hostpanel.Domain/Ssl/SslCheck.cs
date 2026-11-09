using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Ssl;

/// <summary>Cached SSL certificate check result for a domain.</summary>
public sealed class SslCheck : BaseEntity
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

    /// <summary>Required by EF Core; use <see cref="Create"/>.</summary>
    private SslCheck() { }

    /// <summary>Creates a new SSL check result for the given domain.</summary>
    /// <param name="domainName">Domain that was probed.</param>
    /// <param name="hasSSL">Whether a certificate was presented.</param>
    /// <param name="issuer">Certificate issuer, or null.</param>
    /// <param name="expiresAt">Certificate expiry, or null.</param>
    /// <param name="isActive">Whether the domain still belongs to an active service.</param>
    /// <param name="checkedAt">When the probe ran, UTC.</param>
    /// <returns>The new check result.</returns>
    public static SslCheck Create(string domainName, bool hasSSL, string? issuer, DateTimeOffset? expiresAt, bool isActive, DateTimeOffset checkedAt)
        => new()
        {
            DomainName = domainName,
            HasSsl = hasSSL,
            Issuer = issuer,
            ExpiresAt = expiresAt,
            CheckedAt = checkedAt,
            IsActive = isActive,
        };

    /// <summary>Records the outcome of a fresh check of the domain and stamps when it ran.</summary>
    /// <param name="hasSSL">Whether a certificate was presented.</param>
    /// <param name="issuer">Certificate issuer, or null.</param>
    /// <param name="expiresAt">Certificate expiry, or null.</param>
    /// <param name="isActive">Whether the domain still belongs to an active service.</param>
    /// <param name="checkedAt">When the probe ran, UTC.</param>
    public void RecordCheck(bool hasSSL, string? issuer, DateTimeOffset? expiresAt, bool isActive, DateTimeOffset checkedAt)
    {
        HasSsl = hasSSL;
        Issuer = issuer;
        ExpiresAt = expiresAt;
        CheckedAt = checkedAt;
        IsActive = isActive;
    }
}
