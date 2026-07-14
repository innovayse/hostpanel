namespace Innovayse.Application.Email.Commands.VerifyEmailDomainDns;

using Innovayse.Application.Common;
using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email;
using Innovayse.Domain.Email.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Handles <see cref="VerifyEmailDomainDnsCommand"/>.
/// Performs live DNS verification against all required records for the domain.
/// Automatically activates the domain if every record passes.
/// </summary>
/// <remarks>
/// The mail hostname is read from <c>IConfiguration["Mailcow:MailHostname"]</c> so that
/// the Application layer does not take a dependency on Infrastructure types.
/// </remarks>
public sealed class VerifyEmailDomainDnsHandler(
    IEmailDomainRepository repo,
    IDnsVerifier dnsVerifier,
    IUnitOfWork uow,
    IConfiguration configuration)
{
    /// <summary>
    /// Verifies DNS records for the specified email domain.
    /// </summary>
    /// <param name="cmd">The verify command containing the email domain ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="DnsRecordRequirementDto"/> entries, one per required record,
    /// each decorated with the live verification outcome.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the email domain is not found.</exception>
    public async Task<IReadOnlyList<DnsRecordRequirementDto>> HandleAsync(
        VerifyEmailDomainDnsCommand cmd, CancellationToken ct)
    {
        var emailDomain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var hostname = configuration["Mailcow:MailHostname"]
            ?? throw new InvalidOperationException("Mailcow:MailHostname is not configured.");

        var requirements = BuildRequirements(emailDomain, hostname);

        var results = await dnsVerifier.VerifyAsync(emailDomain.DomainName, requirements, ct);

        var dtos = results.Select((r, i) => new DnsRecordRequirementDto(
            requirements[i].Type,
            requirements[i].Name,
            requirements[i].Value,
            requirements[i].Priority,
            r.Verified,
            r.FoundValue,
            r.Error)).ToList();

        // Persist verification results so they survive page reloads
        emailDomain.SetLastDnsVerification(System.Text.Json.JsonSerializer.Serialize(dtos));

        // Activate the domain automatically when all records pass
        var allVerified = results.All(r => r.Verified);
        if (allVerified && emailDomain.Status == EmailDomainStatus.PendingDns)
            emailDomain.Activate();

        await uow.SaveChangesAsync(ct);

        return dtos;
    }

    private static List<DnsRecordRequirement> BuildRequirements(EmailDomain domain, string hostname)
    {
        var records = new List<DnsRecordRequirement>
        {
            new("MX", "@", hostname, 10),
            new("TXT", "@", $"v=spf1 mx a include:{hostname} -all", null),
            new("TXT", "_dmarc", "v=DMARC1; p=quarantine; rua=mailto:dmarc@" + domain.DomainName, null),
        };

        if (domain.DkimPublicKey is not null)
        {
            records.Add(new("TXT", $"{domain.DkimSelector}._domainkey", domain.DkimPublicKey, null));
        }

        records.Add(new("CNAME", "autodiscover", hostname, null));
        records.Add(new("CNAME", "autoconfig", hostname, null));

        return records;
    }
}
