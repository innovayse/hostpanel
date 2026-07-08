namespace Innovayse.Domain.Email;

/// <summary>
/// Describes a single DNS record that must exist for an email domain to be verified.
/// </summary>
/// <param name="Type">DNS record type: "MX", "TXT", or "CNAME".</param>
/// <param name="Name">Record name: "@", "dkim._domainkey", "_dmarc", etc.</param>
/// <param name="Value">Expected record value.</param>
/// <param name="Priority">Priority for MX records; null for other types.</param>
public record DnsRecordRequirement(
    string Type,
    string Name,
    string Value,
    int? Priority);
