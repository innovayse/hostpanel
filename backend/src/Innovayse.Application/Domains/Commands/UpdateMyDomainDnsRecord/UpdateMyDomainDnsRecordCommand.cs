namespace Innovayse.Application.Domains.Commands.UpdateMyDomainDnsRecord;

/// <summary>Command for a client to update a DNS record on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>UpdateDnsRecordCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="RecordId">Primary key of the DNS record to update.</param>
/// <param name="Value">New record value.</param>
/// <param name="Ttl">New time-to-live in seconds.</param>
/// <param name="Priority">New priority for MX/SRV records; null to clear.</param>
public sealed record UpdateMyDomainDnsRecordCommand(
    int DomainId,
    int RecordId,
    string Value,
    int Ttl,
    int? Priority);
