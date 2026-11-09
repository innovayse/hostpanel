using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.SetRegistrarLock;

/// <summary>
/// Enables or disables the registrar transfer-lock for a domain at both the registrar and
/// aggregate levels.
/// </summary>
public sealed class SetRegistrarLockHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<SetRegistrarLockHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public SetRegistrarLockHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<SetRegistrarLockHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="SetRegistrarLockCommand"/>.
    /// </summary>
    /// <param name="cmd">The set registrar lock command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>REGISTRAR_LOCK_UPSTREAM_FAILED</c> when the registrar rejects the update.
    /// </returns>
    public async Task<Result> HandleAsync(SetRegistrarLockCommand cmd, CancellationToken ct)
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
            var result = await registrar.SetRegistrarLockAsync(
                new RegistrarFlagChange(new DomainRef(domain.Name, domain.RegistrarRef), cmd.Enabled), ct);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Registrar failed to update lock status for '{DomainName}': {Reason}",
                    domain.Name, result.ErrorCode);
                return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.REGISTRAR_LOCK_UPSTREAM_FAILED));
            }
        }

        domain.SetLock(cmd.Enabled);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
