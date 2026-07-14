namespace Innovayse.Application.Email.Queries.GetRequiredDnsRecords;

/// <summary>
/// Returns the set of DNS records that must be published for the given email domain to function.
/// Unlike <c>VerifyEmailDomainDnsCommand</c>, this query does not perform live DNS lookups;
/// it returns the expected values for the user to configure in their DNS provider.
/// </summary>
public record GetRequiredDnsRecordsQuery(int EmailDomainId);
