namespace Innovayse.Application.Reports.DTOs;

/// <summary>One row in the SSL Monitoring report.</summary>
/// <param name="DomainName">Domain the certificate was checked for.</param>
/// <param name="HasSsl">Whether a certificate was served at all.</param>
/// <param name="Issuer">Certificate authority that issued it, when known.</param>
/// <param name="ExpiresAt">Expiry as already formatted for display, or null when unknown.</param>
/// <param name="LastUpdate">When this row was last refreshed, as formatted for display.</param>
/// <param name="IsActive">Whether the domain itself is still active in the panel.</param>
public record SslDomainRowDto(
    string DomainName,
    bool HasSsl,
    string? Issuer,
    string? ExpiresAt,
    string LastUpdate,
    bool IsActive);
