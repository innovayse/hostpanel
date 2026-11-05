using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultStoredMethod;

/// <summary>
/// Handles <see cref="SetDefaultStoredMethodCommand"/>: asks the payment plugin that holds the method to make it the
/// customer's default, and records the same choice on the consent, which is the default the host charges first.
/// </summary>
/// <remarks>
/// Only a method the caller has an active consent for can be made the default, so a foreign or invented id never
/// reaches the plugin. The host's choice is saved only after the plugin accepted it, so the two never disagree because
/// the gateway refused; the consent clears the flag on the client's other consents under the module.
/// </remarks>
public sealed class SetDefaultStoredMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SetDefaultStoredMethodHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="logger">Structured logger.</param>
    public SetDefaultStoredMethodHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        ILogger<SetDefaultStoredMethodHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once the plugin made the method the default; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client
    /// record; <c>CARD_CONSENT_NOT_FOUND</c> when the id is not an active consent of the caller (revoked, or its
    /// save session never completed); <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the plugin is
    /// off or offers no stored credentials; <c>NO_SAVED_PAYMENT_METHODS</c> when the plugin holds no customer for the
    /// client; or <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin cannot be reached.
    /// </returns>
    public async Task<Result> HandleAsync(SetDefaultStoredMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result.FailureCode(clientId.ErrorCode!);
        }

        var consent = await _context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.Id == cmd.ConsentId && c.ClientId == clientId.Value, ct);
        if (consent is null || !consent.IsActive)
        {
            return Result.FailureCode(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
        }

        var plugin = await _plugins.ResolveAsync(consent.GatewayModuleId, ct);
        if (plugin is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        }

        var customerRef = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == consent.ClientId && c.GatewayModuleId == consent.GatewayModuleId)
            .Select(c => c.CustomerRef)
            .FirstOrDefaultAsync(ct);
        if (customerRef is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        }

        try
        {
            await plugin.SetDefaultMethodAsync(customerRef, consent.MethodRef!, ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogWarning(
                ex, "Payment plugin {Module} could not make a stored method the default of client {ClientId}.",
                consent.GatewayModuleId, consent.ClientId);
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        var siblings = await _context.CardChargeConsents
            .Where(c => c.ClientId == consent.ClientId && c.GatewayModuleId == consent.GatewayModuleId && c.Id != consent.Id && c.IsDefault)
            .ToListAsync(ct);
        consent.SetDefault(siblings);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
