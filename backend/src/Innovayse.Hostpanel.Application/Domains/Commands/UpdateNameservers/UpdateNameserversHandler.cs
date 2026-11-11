using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.UpdateNameservers;

/// <summary>
/// Replaces the nameserver list for a domain at both the registrar and aggregate levels.
/// </summary>
public sealed class UpdateNameserversHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<UpdateNameserversHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public UpdateNameserversHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<UpdateNameserversHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="UpdateNameserversCommand"/>.
    /// </summary>
    /// <param name="cmd">The update nameservers command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>NAMESERVERS_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateNameserversCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId!.Value, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        if (domain.RegistrarRef is not null)
        {
            var registrar = await _registrars.ResolveAsync(domain, ct);
            var result = await registrar.SetNameserversAsync(
                new NameserverChange(new DomainRef(domain.Name, domain.RegistrarRef), cmd.Nameservers), ct);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Registrar failed to update nameservers for '{DomainName}': {Reason}",
                    domain.Name, result.ErrorCode);
                return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.NAMESERVERS_UPSTREAM_FAILED));
            }
        }

        domain.SetNameservers(cmd.Nameservers);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
