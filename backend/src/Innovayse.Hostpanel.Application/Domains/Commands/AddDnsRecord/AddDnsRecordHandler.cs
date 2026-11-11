using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.AddDnsRecord;

/// <summary>
/// Adds a new DNS record to a domain's zone both in the aggregate and at the registrar.
/// The record is added to the aggregate first so its generated ID is available when calling the registrar.
/// </summary>
public sealed class AddDnsRecordHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<AddDnsRecordHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public AddDnsRecordHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<AddDnsRecordHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="AddDnsRecordCommand"/>.
    /// </summary>
    /// <param name="cmd">The add DNS record command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DNS_RECORD_UPSTREAM_FAILED</c> when the registrar rejects the addition.
    /// </returns>
    public async Task<Result> HandleAsync(AddDnsRecordCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        domain.AddDnsRecord(cmd.Type, cmd.Host, cmd.Value, cmd.Ttl, cmd.Priority);

        if (domain.RegistrarRef is not null)
        {
            var newRecord = domain.DnsRecords[^1];
            var registrar = await _registrars.ResolveAsync(domain, ct);
            var result = await registrar.AddDnsRecordAsync(
                new DnsRecordChange(new DomainRef(domain.Name, domain.RegistrarRef), newRecord), ct);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Registrar failed to add DNS record for '{DomainName}': {Reason}",
                    domain.Name, result.ErrorCode);
                return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DNS_RECORD_UPSTREAM_FAILED));
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
