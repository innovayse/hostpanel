namespace Innovayse.Application.Email.Queries.GetRequiredDnsRecords;

using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Handles <see cref="GetRequiredDnsRecordsQuery"/>.
/// Returns the expected DNS record values that the client must publish — no live DNS lookups.
/// </summary>
/// <remarks>
/// The mail hostname is read from <c>IConfiguration["Mailcow:MailHostname"]</c> so that
/// the Application layer does not take a dependency on Infrastructure types.
/// </remarks>
public sealed class GetRequiredDnsRecordsHandler(
    IEmailDomainRepository repo,
    IConfiguration configuration)
{
    /// <summary>
    /// Returns all required DNS records for the specified email domain.
    /// </summary>
    /// <param name="query">Query containing the email domain ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ordered list of DNS record requirements without verification data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the email domain is not found.</exception>
    public async Task<IReadOnlyList<DnsRecordRequirementDto>> HandleAsync(
        GetRequiredDnsRecordsQuery query, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(query.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {query.EmailDomainId} not found.");

        // Return persisted verification results if available
        if (domain.LastDnsVerificationJson is not null)
        {
            var cached = System.Text.Json.JsonSerializer.Deserialize<List<DnsRecordRequirementDto>>(
                domain.LastDnsVerificationJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cached is not null)
                return cached;
        }

        var hostname = configuration["Mailcow:MailHostname"]
            ?? throw new InvalidOperationException("Mailcow:MailHostname is not configured.");

        var records = new List<DnsRecordRequirementDto>
        {
            new("MX", "@", hostname, 10, null, null, null),
            new("TXT", "@", $"v=spf1 mx a include:{hostname} -all", null, null, null, null),
            new("TXT", "_dmarc", "v=DMARC1; p=quarantine; rua=mailto:dmarc@" + domain.DomainName, null, null, null, null),
        };

        if (domain.DkimPublicKey is not null)
        {
            records.Add(new("TXT", $"{domain.DkimSelector}._domainkey", domain.DkimPublicKey, null, null, null, null));
        }

        records.Add(new("CNAME", "autodiscover", hostname, null, null, null, null));
        records.Add(new("CNAME", "autoconfig", hostname, null, null, null, null));

        return records;
    }
}
