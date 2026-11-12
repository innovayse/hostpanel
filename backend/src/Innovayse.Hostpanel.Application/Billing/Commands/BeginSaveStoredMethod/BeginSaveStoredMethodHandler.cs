using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginSaveStoredMethod;

/// <summary>
/// Handles <see cref="BeginSaveStoredMethodCommand"/>: finds or creates the client's customer at the gateway module,
/// opens a save session there and records the client's consent for it.
/// </summary>
/// <remarks>
/// The consent is written right after the session is opened and before the payer enters anything at the gateway, so
/// a method can only ever be stored for a client who agreed to it. The consent keeps the wording version the payer
/// ticked and the address and browser of the request, as evidence. It has no method yet: completing the session binds
/// one (<c>CompleteSaveStoredMethodCommand</c>). The host keeps one customer per client and module in
/// <c>GatewayCustomers</c>, found or created by <see cref="GatewayCustomerEnsurer"/>.
/// </remarks>
public sealed class BeginSaveStoredMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>The Wolverine bus, for the Clients module's status checks.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Finds or creates the client's customer at the gateway module.</summary>
    private readonly GatewayCustomerEnsurer _customers;

    /// <summary>The origins a payer may be handed back to, used to validate <c>ReturnUrl</c>.</summary>
    private readonly IOptions<CorsOptions> _returnUrlOptions;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<BeginSaveStoredMethodHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="bus">The Wolverine bus, for the Clients module's status checks.</param>
    /// <param name="customers">Finds or creates the client's customer at the gateway module.</param>
    /// <param name="returnUrlOptions">The origins a payer may be handed back to.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public BeginSaveStoredMethodHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        IMessageBus bus,
        GatewayCustomerEnsurer customers,
        IOptions<CorsOptions> returnUrlOptions,
        IClock clock,
        ILogger<BeginSaveStoredMethodHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _bus = bus;
        _customers = customers;
        _returnUrlOptions = returnUrlOptions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The save session; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record;
    /// <c>CLIENT_SUSPENDED</c> / <c>CLIENT_CLOSED</c> / <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not
    /// save a method; <c>RETURN_URL_NOT_ALLOWED</c> when the address's origin is not allowed;
    /// <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the module is off or offers no stored credentials; or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin could not be reached (no consent and no customer are recorded then).
    /// </returns>
    public async Task<Result<SaveMethodSessionDto>> HandleAsync(BeginSaveStoredMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<SaveMethodSessionDto>.FailureCode(clientId.ErrorCode!);
        }

        var mayTransact = await _bus.EnsureClientMayTransactAsync(clientId.Value, ct);
        if (mayTransact.IsFailure)
        {
            return Result<SaveMethodSessionDto>.FailureCode(mayTransact.ErrorCode!);
        }

        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(clientId.Value, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<SaveMethodSessionDto>.FailureCode(mayPayByCard.ErrorCode!);
        }

        if (!ReturnUrlPolicy.IsAllowed(cmd.ReturnUrl, _returnUrlOptions.Value))
        {
            return Result<SaveMethodSessionDto>.FailureCode(HostpanelErrorCodes.RETURN_URL_NOT_ALLOWED);
        }

        var plugin = await _plugins.ResolveAsync(cmd.GatewayModuleId, ct);
        if (plugin is null)
        {
            return Result<SaveMethodSessionDto>.FailureCode(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        }

        try
        {
            var customer = await _customers.EnsureAsync(plugin, clientId.Value, cmd.GatewayModuleId, ct);
            if (customer is null)
            {
                return Result<SaveMethodSessionDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
            }

            var session = await plugin.BeginSaveMethodAsync(new SaveMethodContext(customer.CustomerRef, cmd.ReturnUrl), ct);

            // Recorded before the payer is handed to the gateway step, so nothing can be stored without consent. The customer
            // row the session needs is saved with it, so a session that could not be opened leaves no customer behind.
            _context.CardChargeConsents.Add(
                CardChargeConsent.Grant(
                    clientId.Value, cmd.GatewayModuleId, session.SessionId, _clock.UtcNow, cmd.ConsentTextVersion, cmd.IpAddress, cmd.UserAgent));
            await _customers.SaveAsync(customer, ct);

            return Result.Success(new SaveMethodSessionDto(session.SessionId, session.Mode, session.ClientToken, session.RedirectUrl));
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct) && ex is not ConcurrencyConflictException)
        {
            // A plugin's exceptions are its own types, so a refusal cannot be told from an outage: either way no
            // session was opened and the payer may try again. The gateway's text stays in the log.
            _logger.LogWarning(
                ex, "Payment plugin {Module} could not open a save session for client {ClientId}.", cmd.GatewayModuleId, clientId.Value);
            return Result<SaveMethodSessionDto>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }
    }
}
