using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RevokeCardChargeConsent;

/// <summary>
/// Handles <see cref="RevokeCardChargeConsentCommand"/>: revokes the consent, which is final, and then asks the
/// plugin to remove the stored method.
/// </summary>
/// <remarks>
/// The revocation is saved first. Removing the method at the gateway is a courtesy that can fail (the gateway is down,
/// the method is already gone); it is logged and never undoes the revocation, because the host's own check is what
/// keeps the method from being charged.
/// </remarks>
public sealed class RevokeCardChargeConsentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<RevokeCardChargeConsentHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public RevokeCardChargeConsentHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        IClock clock,
        ILogger<RevokeCardChargeConsentHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the consent is revoked, or at once when it already was;
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record; or <c>CARD_CONSENT_NOT_FOUND</c> when the
    /// id is not a consent of the caller.
    /// </returns>
    public async Task<Result> HandleAsync(RevokeCardChargeConsentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result.FailureCode(clientId.ErrorCode!);
        }

        var consent = await _context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.Id == cmd.ConsentId && c.ClientId == clientId.Value, ct);
        if (consent is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
        }

        if (consent.IsRevoked)
        {
            return Result.Success();
        }

        consent.Revoke(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);

        await RemoveMethodAsync(consent.ClientId, consent.GatewayModuleId, consent.MethodRef, ct);
        return Result.Success();
    }

    /// <summary>Asks the plugin to remove the method, when the consent had one and the client's customer is known.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="methodRef">The method the consent covered, or <see langword="null"/> if the session never completed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the attempt is over; a failure is logged, not thrown.</returns>
    private async Task RemoveMethodAsync(int clientId, string module, string? methodRef, CancellationToken ct)
    {
        if (methodRef is null)
        {
            return;
        }

        var customerRef = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == module)
            .Select(c => c.CustomerRef)
            .FirstOrDefaultAsync(ct);
        if (customerRef is null)
        {
            return;
        }

        var plugin = await _plugins.ResolveAsync(module, ct);
        if (plugin is null)
        {
            return;
        }

        try
        {
            await plugin.RemoveMethodAsync(customerRef, methodRef, ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogWarning(
                ex, "Payment plugin {Module} could not remove a stored method of client {ClientId}; the consent is revoked anyway.", module, clientId);
        }
    }
}
