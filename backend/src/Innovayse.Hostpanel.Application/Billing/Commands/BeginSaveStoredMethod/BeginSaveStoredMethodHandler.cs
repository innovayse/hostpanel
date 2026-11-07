using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;
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
/// <c>GatewayCustomers</c>; the plugin is asked for it only the first time.
/// </remarks>
public sealed class BeginSaveStoredMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves which client the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>The Wolverine bus, for the Clients module's status checks and brief.</summary>
    private readonly IMessageBus _bus;

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
    /// <param name="bus">The Wolverine bus, for the Clients module's status checks and brief.</param>
    /// <param name="returnUrlOptions">The origins a payer may be handed back to.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public BeginSaveStoredMethodHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IStoredCredentialPluginResolver plugins,
        IMessageBus bus,
        IOptions<CorsOptions> returnUrlOptions,
        IClock clock,
        ILogger<BeginSaveStoredMethodHandler> logger)
    {
        _context = context;
        _access = access;
        _plugins = plugins;
        _bus = bus;
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
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin could not be reached (no consent is recorded then).
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
            var customerRef = await EnsureCustomerAsync(plugin, clientId.Value, cmd.GatewayModuleId, ct);
            if (customerRef is null)
            {
                return Result<SaveMethodSessionDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
            }

            var session = await plugin.BeginSaveMethodAsync(new SaveMethodContext(customerRef, cmd.ReturnUrl), ct);

            // Recorded before the payer is handed to the gateway step, so nothing can be stored without consent.
            _context.CardChargeConsents.Add(
                CardChargeConsent.Grant(
                    clientId.Value, cmd.GatewayModuleId, session.SessionId, _clock.UtcNow, cmd.ConsentTextVersion, cmd.IpAddress, cmd.UserAgent));
            await _context.SaveChangesAsync(ct);

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

    /// <summary>
    /// Returns the client's customer reference at the module, asking the plugin and recording it the first time. Two
    /// first requests at once end with the one the database kept.
    /// </summary>
    /// <param name="plugin">The stored credential capability of the module.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer reference, or <see langword="null"/> when the client has no record to build the customer from.</returns>
    private async Task<string?> EnsureCustomerAsync(IStoredCredentialPlugin plugin, int clientId, string module, CancellationToken ct)
    {
        var existing = await _context.GatewayCustomers
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == module)
            .Select(c => c.CustomerRef)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var client = await _bus.FindClientBriefAsync(clientId, withEmail: true, ct);
        if (client is null)
        {
            return null;
        }

        var created = await plugin.EnsureCustomerAsync(
            new CredentialCustomerContext(ClientReference(clientId), client.Email, client.FullName), ct);

        _context.GatewayCustomers.Add(GatewayCustomer.Create(clientId, module, created.CustomerRef, _clock.UtcNow));
        try
        {
            await _context.SaveChangesAsync(ct);
            return created.CustomerRef;
        }
        catch (ConcurrencyConflictException)
        {
            // Another request recorded the customer first. The plugin answers the same customer for the same client
            // reference, so the row that won holds the reference this request would have stored.
            _context.DetachAll();
            return await _context.GatewayCustomers
                .AsNoTracking()
                .Where(c => c.ClientId == clientId && c.GatewayModuleId == module)
                .Select(c => c.CustomerRef)
                .FirstAsync(ct);
        }
    }

    /// <summary>The host's stable reference for a client, opaque to the plugin.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The reference the plugin is asked about, the same on every call for the client.</returns>
    private static string ClientReference(int clientId) => string.Create(CultureInfo.InvariantCulture, $"client-{clientId}");
}
