using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CompleteSaveStoredMethod;

/// <summary>
/// Handles <see cref="CompleteSaveStoredMethodCommand"/>: asks the plugin what the session stored and binds that
/// method to the consent recorded when the session was opened.
/// </summary>
/// <remarks>
/// The plugin is asked for the session id alone and completing a session is idempotent on its side, so a repeat of
/// this command (a double click, a payer returning twice) answers the same method and binds nothing twice.
/// A consent that already covers a different method is not rebound: the answer is a failure. When the client already
/// holds an active consent for the same method (saved again in a new session), the older one is revoked in the same
/// save, so one method is covered by one consent; it hands its default flag to the new one. The method itself stays at
/// the gateway, because it is the very method the new consent covers. Binding is <see cref="StoredMethodBinder"/>'s,
/// shared with a payment that saves its method.
/// </remarks>
public sealed class CompleteSaveStoredMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>Binds the stored method to the consent and retires the consents it supersedes.</summary>
    private readonly StoredMethodBinder _binder;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<CompleteSaveStoredMethodHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="binder">Binds the stored method to the consent and retires the consents it supersedes.</param>
    /// <param name="logger">Structured logger.</param>
    public CompleteSaveStoredMethodHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        StoredMethodBinder binder,
        ILogger<CompleteSaveStoredMethodHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _binder = binder;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The stored method; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record;
    /// <c>CARD_CONSENT_NOT_FOUND</c> when the session is not one the caller opened;
    /// <c>CARD_CONSENT_REQUIRED</c> when the consent was revoked meanwhile;
    /// <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the module is off or offers no stored credentials;
    /// <c>STORED_METHOD_NOT_SAVED</c> when the payer abandoned the session;
    /// <c>CARD_CONSENT_ALREADY_ATTACHED</c> when the consent already covers a different method; or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin could not be reached (the same session may be completed again).
    /// </returns>
    public async Task<Result<StoredMethodDto>> HandleAsync(CompleteSaveStoredMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<StoredMethodDto>.FailureCode(clientId.ErrorCode!);
        }

        // Found by the caller's own client id as well as the session, so a session id of someone else's is no consent.
        var consent = await _context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.ClientId == clientId.Value && c.GatewayModuleId == cmd.GatewayModuleId && c.SessionId == cmd.SessionId, ct);
        if (consent is null)
        {
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
        }

        if (consent.IsRevoked)
        {
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        }

        var plugin = await _plugins.ResolveAsync(cmd.GatewayModuleId, ct);
        if (plugin is null)
        {
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        }

        SavedMethodResult saved;
        try
        {
            saved = await plugin.CompleteSaveMethodAsync(cmd.SessionId, ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogWarning(
                ex, "Payment plugin {Module} could not complete save session for client {ClientId}.", cmd.GatewayModuleId, clientId.Value);
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        if (!saved.Saved || saved.Method is null)
        {
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.STORED_METHOD_NOT_SAVED);
        }

        if (!await _binder.TryBindAsync(consent, saved.Method.MethodRef, ct))
        {
            return Result<StoredMethodDto>.FailureCode(HostpanelErrorCodes.CARD_CONSENT_ALREADY_ATTACHED);
        }

        await _context.SaveChangesAsync(ct);

        var method = saved.Method;
        return Result.Success(new StoredMethodDto(
            consent.Id, consent.GatewayModuleId, method.MethodRef, method.Brand, method.Last4, method.ExpiryMonth, method.ExpiryYear, consent.IsDefault));
    }
}
