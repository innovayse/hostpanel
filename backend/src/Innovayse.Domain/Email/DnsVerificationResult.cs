namespace Innovayse.Domain.Email;

/// <summary>
/// Represents the outcome of verifying a single DNS record requirement.
/// </summary>
/// <param name="RecordName">The DNS record name that was checked.</param>
/// <param name="ExpectedType">The DNS record type that was expected (e.g. "MX", "TXT").</param>
/// <param name="Verified">Whether the record was found and matched the expected value.</param>
/// <param name="FoundValue">The actual value found in DNS, if any.</param>
/// <param name="Error">Error message if the lookup failed; null on success.</param>
public record DnsVerificationResult(
    string RecordName,
    string ExpectedType,
    bool Verified,
    string? FoundValue,
    string? Error);
