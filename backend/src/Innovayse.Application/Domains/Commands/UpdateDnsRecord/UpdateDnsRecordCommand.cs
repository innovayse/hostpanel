namespace Innovayse.Application.Domains.Commands.UpdateDnsRecord;

/// <summary>Command to update the value, TTL, and priority of an existing DNS record.</summary>
/// <param name="DomainId">Primary key of the domain that owns the record.</param>
/// <param name="RecordId">Primary key of the DNS record to update.</param>
/// <param name="Value">New record value.</param>
/// <param name="Ttl">New time-to-live in seconds.</param>
/// <param name="Priority">New priority for MX/SRV records; null to clear.</param>
public record UpdateDnsRecordCommand(int DomainId, int RecordId, string Value, int Ttl, int? Priority);
