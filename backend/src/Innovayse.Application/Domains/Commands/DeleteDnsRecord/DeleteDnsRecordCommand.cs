namespace Innovayse.Application.Domains.Commands.DeleteDnsRecord;

/// <summary>Command to remove a DNS record from a domain's zone.</summary>
/// <param name="DomainId">Primary key of the domain that owns the record.</param>
/// <param name="RecordId">Primary key of the DNS record to delete.</param>
public record DeleteDnsRecordCommand(int DomainId, int RecordId);
