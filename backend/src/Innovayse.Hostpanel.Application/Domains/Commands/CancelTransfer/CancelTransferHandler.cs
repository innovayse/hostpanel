using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Domains.Commands.CancelTransfer;

/// <summary>
/// Cancels a pending incoming domain transfer at the registrar and marks the domain aggregate as cancelled.
/// </summary>
public sealed class CancelTransferHandler
{
    /// <summary>Resolves the registrar that holds the domain's TLD.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>The data context the domain aggregate is loaded, mutated and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Logger that records the registrar's refusal code for operators.</summary>
    private readonly ILogger<CancelTransferHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registrars">Resolves the registrar that holds the domain's TLD.</param>
    /// <param name="context">The data context the domain aggregate is loaded, mutated and saved through.</param>
    /// <param name="logger">Logger that records the registrar's refusal code for operators.</param>
    public CancelTransferHandler(
        RegistrarProviderResolver registrars,
        IHostpanelDbContext context,
        ILogger<CancelTransferHandler> logger)
    {
        _registrars = registrars;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Handles <see cref="CancelTransferCommand"/>.
    /// </summary>
    /// <param name="cmd">The cancel transfer command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>REGISTRAR_OPERATION_UNSUPPORTED</c> when the registrar offers no programmatic cancellation, or
    /// <c>DOMAIN_TRANSFER_CANCEL_UPSTREAM_FAILED</c> when the registrar rejects the cancellation.
    /// </returns>
    public async Task<Result> HandleAsync(CancelTransferCommand cmd, CancellationToken ct)
    {
        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Id == cmd.DomainId, ct);
        if (domain is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        var registrar = await _registrars.ResolveAsync(domain, ct);
        var result = await registrar.CancelTransferAsync(
            new DomainRef(domain.Name, domain.RegistrarRef ?? string.Empty), ct);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Registrar rejected transfer cancellation for '{DomainName}': {Reason}",
                domain.Name, result.ErrorCode);
            return Result.FailureCode(result.ToFailureCode(HostpanelErrorCodes.DOMAIN_TRANSFER_CANCEL_UPSTREAM_FAILED));
        }

        domain.Cancel();
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
