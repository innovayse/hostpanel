using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.DeleteDnsRecord;

/// <summary>
/// Removes a DNS record from the domain aggregate and deletes it at the registrar.
/// </summary>
public sealed class DeleteDnsRecordHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<DeleteDnsRecordHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public DeleteDnsRecordHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<DeleteDnsRecordHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="DeleteDnsRecordCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete DNS record command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DNS_RECORD_NOT_FOUND</c> when the record id names no record on that domain, or
    /// <c>DNS_RECORD_UPSTREAM_FAILED</c> when the registrar rejects the deletion.
    /// </returns>
    public async Task<Result> HandleAsync(DeleteDnsRecordCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.DnsRecords.All(r => r.Id != cmd.RecordId!.Value))
        {
            return Result.FailureCode(HostpanelErrorCodes.DNS_RECORD_NOT_FOUND);
        }

        domain.RemoveDnsRecord(cmd.RecordId!.Value);

        if (domain.RegistrarRef is not null)
        {
            var registrar = await _registrars.ResolveAsync(domain, ct);
            var result = await registrar.DeleteDnsRecordAsync(
                new DnsRecordRemoval(new DomainRef(domain.Name, domain.RegistrarRef), cmd.RecordId!.Value), ct);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Registrar failed to delete DNS record {RecordId} for '{DomainName}': {Reason}",
                    cmd.RecordId, domain.Name, result.ErrorCode);
                return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DNS_RECORD_UPSTREAM_FAILED));
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
