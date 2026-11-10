using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetDnsManagement;

/// <summary>
/// Enables or disables DNS management for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class SetDnsManagementHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<SetDnsManagementHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public SetDnsManagementHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<SetDnsManagementHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="SetDnsManagementCommand"/>.
    /// </summary>
    /// <param name="cmd">The set DNS management command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DNS_MANAGEMENT_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    public async Task<Result> HandleAsync(SetDnsManagementCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.SetDnsManagementAsync(
            new DomainFeatureToggle(domain.Name, cmd.Enabled), ct);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar failed to update DNS management for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DNS_MANAGEMENT_UPSTREAM_FAILED));
        }

        domain.SetDnsManagement(cmd.Enabled);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
