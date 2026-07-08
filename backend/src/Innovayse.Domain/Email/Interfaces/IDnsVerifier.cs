namespace Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Port for verifying that DNS records required by an email domain are correctly published.
/// Implemented in Infrastructure using a real DNS resolver.
/// </summary>
public interface IDnsVerifier
{
    /// <summary>
    /// Verifies that all required DNS records are present and correctly configured.
    /// </summary>
    /// <param name="domainName">The domain name to verify DNS records for.</param>
    /// <param name="requirements">The set of DNS records that must exist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// One <see cref="DnsVerificationResult"/> per requirement, indicating whether
    /// each record was found and matched the expected value.
    /// </returns>
    Task<IReadOnlyList<DnsVerificationResult>> VerifyAsync(
        string domainName,
        IReadOnlyList<DnsRecordRequirement> requirements,
        CancellationToken ct);
}
