using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;
namespace Innovayse.Hostpanel.Application.Billing.Commands.StartGatewayPayment;

/// <summary>
/// Handles <see cref="StartGatewayPaymentCommand"/>: resolves the payment plugin,
/// registers the payment (fresh per-attempt order number; the gateway rejects
/// reused ones), stores the session on the invoice, and returns the redirect URL. The gateway order of
/// a session the new one replaces is cancelled through its plugin when the plugin can (<see cref="GatewayPaymentCanceller"/>).
/// </summary>
/// <remarks>
/// When the payer asked to keep the payment method (<see cref="StartGatewayPaymentCommand.SavePaymentMethod"/>) the
/// payment is registered with that request and the client's consent to later charges is recorded in the very save that
/// records the session, so a consent only exists for a payment the gateway registered. Only the invoice's own client
/// can ask for it, a linked user only where they hold <c>ViewPayInvoices</c>, at a module whose plugin offers stored
/// credentials, and the gateway customer the later charges need is ensured before the payment is registered and recorded
/// in that same save. Completing the payment binds the method to that consent (<c>CompleteGatewayPaymentHandler</c>); a
/// consent whose session is replaced by this attempt is revoked here, with the new session.
/// </remarks>
public sealed class StartGatewayPaymentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the configured payment plugin for a module id.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>The origins a payer may be handed back to, used to validate <c>ReturnUrl</c>.</summary>
    private readonly IOptions<CorsOptions> _returnUrlOptions;

    /// <summary>
    /// Structured logger, used to record gateway status-probe failures during the live-session
    /// check and payment registrations the plugin failed.
    /// </summary>
    private readonly ILogger<StartGatewayPaymentHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Wolverine bus, used to ask the Clients module whether the invoice's account may pay.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Voids the gateway order of the session this attempt replaces.</summary>
    private readonly GatewayPaymentCanceller _canceller;

    /// <summary>Tells whether the request carries a caller at all, for a payment that saves its method.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Resolves which client the caller acts for, for a payment that saves its method.</summary>
    private readonly IClientAccess _access;

    /// <summary>Finds or creates the client's customer at the gateway module before a payment that saves its method is registered.</summary>
    private readonly GatewayCustomerEnsurer _customers;

    /// <summary>Ends the consent of the session this attempt replaces.</summary>
    private readonly UnboundConsentRevoker _consents;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Resolves the configured payment plugin for a module id.</param>
    /// <param name="returnUrlOptions">The origins a payer may be handed back to.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="bus">Wolverine bus, used to ask the Clients module whether the invoice's account may pay.</param>
    /// <param name="canceller">Voids the gateway order of the session this attempt replaces.</param>
    /// <param name="caller">Tells whether the request carries a caller at all.</param>
    /// <param name="access">Resolves which client the caller acts for.</param>
    /// <param name="customers">Finds or creates the client's customer at the gateway module.</param>
    /// <param name="consents">Ends the consent of the session this attempt replaces.</param>
    public StartGatewayPaymentHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        IOptions<CorsOptions> returnUrlOptions,
        ILogger<StartGatewayPaymentHandler> logger,
        IClock clock,
        IMessageBus bus,
        GatewayPaymentCanceller canceller,
        ICurrentUserService caller,
        IClientAccess access,
        GatewayCustomerEnsurer customers,
        UnboundConsentRevoker consents)
    {
        _context = context;
        _pluginResolver = pluginResolver;
        _returnUrlOptions = returnUrlOptions;
        _logger = logger;
        _clock = clock;
        _bus = bus;
        _canceller = canceller;
        _caller = caller;
        _access = access;
        _customers = customers;
        _consents = consents;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The start command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The gateway redirect URL for the payer's browser, or one of: <c>INVOICE_NOT_FOUND</c>,
    /// <c>INVOICE_NOT_PAYABLE</c>, <c>INVOICE_STORED_CHARGE_UNRESOLVED</c> or <c>INVOICE_STORED_CHARGE_HELD</c> when a
    /// stored card charge claim of the invoice blocks it, <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c>, <c>RETURN_URL_NOT_ALLOWED</c>,
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c>, <c>GATEWAY_SESSION_ALREADY_STARTED</c>,
    /// <c>CURRENCY_NOT_FOUND</c>, <c>GATEWAY_CURRENCY_MISMATCH</c> or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin fails to register the payment. A payment that saves
    /// the method is also refused with <c>UNAUTHORIZED</c> when the request carries no caller (a guest), with
    /// <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the module's plugin offers no stored credentials, with the caller's own failure
    /// (<c>CLIENT_PROFILE_NOT_FOUND</c>, <c>IDENTITY_PLUGIN_UNAVAILABLE</c>)
    /// when the caller acts for no account, with <c>INVOICE_NOT_FOUND</c> when the invoice is another client's,
    /// with <c>CLIENT_PERMISSION_DENIED</c> when a linked user without <c>ViewPayInvoices</c> asks to save, and with
    /// <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not pay by card.
    /// </returns>
    public async Task<Result<RedirectDto>> HandleAsync(StartGatewayPaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var now = _clock.UtcNow;
        var invoice = await _context.Invoices.Include(i => i.Transactions).FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Check payability before touching the bank at all: starting a session for a
        // Paid/Cancelled/Refunded invoice would register a live payable order at the gateway
        // for nothing, since SetGatewaySession() would reject it afterwards anyway.
        if (!invoice.IsPayable)
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        // A stored charge claim (in flight, or held for an operator) owns the invoice: a hosted session beside it
        // could take the money a second time.
        var blockedBy = await _context.InvoicePaymentClaims.BlockingClaimErrorAsync(invoice.Id, ct);
        if (blockedBy is not null)
        {
            return Result<RedirectDto>.FailureCode(blockedBy);
        }

        // A suspended or closed account opens no new gateway session.
        var mayPay = await _bus.EnsureClientMayTransactAsync(invoice.ClientId, ct);
        if (mayPay.IsFailure)
        {
            return Result<RedirectDto>.FailureCode(mayPay.ErrorCode!);
        }

        if (!ReturnUrlPolicy.IsAllowed(cmd.ReturnUrl, _returnUrlOptions.Value))
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.RETURN_URL_NOT_ALLOWED);
        }

        var plugin = await _pluginResolver.ResolveAsync(cmd.Module, ct);
        if (plugin is null)
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.PAYMENT_PLUGIN_NOT_CONFIGURED);
        }

        if (await HasLiveSessionAsync(invoice, now, ct))
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.GATEWAY_SESSION_ALREADY_STARTED);
        }

        var invoiceCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == invoice.Currency, ct);
        if (invoiceCurrency is null)
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        }

        if (!plugin.AcceptsCurrency(invoiceCurrency.Numeric))
        {
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.GATEWAY_CURRENCY_MISMATCH);
        }

        // The checks and the customer come before the payment is registered: a payment the gateway registered for a
        // method nobody may save would be money taken with no consent, and later charges need the customer.
        EnsuredGatewayCustomer? consenting = null;
        if (cmd.SavePaymentMethod)
        {
            var saving = await PrepareSavingAsync(plugin, cmd.Module, invoice, ct);
            if (saving.IsFailure)
            {
                return Result<RedirectDto>.FailureCode(saving.ErrorCode!);
            }

            consenting = saving.Value;
        }

        // Unique per attempt (gateway errorCode 1 on reuse); ≤ 25 chars fits int ids + unix seconds.
        var orderNumber = $"INV{invoice.Id}-{now.ToUnixTimeSeconds()}";

        // The Application layer owns the major to minor conversion, by the currency's ISO 4217 exponent.
        var amountMinor = CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency);

        PaymentSession session;
        try
        {
            session = await plugin.CreatePaymentAsync(
                new PaymentInstruction(orderNumber, amountMinor, cmd.ReturnUrl, $"Invoice #{invoice.Id}", null, cmd.SavePaymentMethod),
                ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // A plugin's exceptions are its own types, so a refusal cannot be told from an outage:
            // either way no session was registered and the payer may try again. The gateway's text
            // stays in the log; the response carries only the code.
            _logger.LogWarning(
                ex,
                "Payment plugin {Module} could not register a payment for invoice {InvoiceId}.",
                cmd.Module, invoice.Id);
            return Result<RedirectDto>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        // The session this attempt replaces is abandoned. Read before it is overwritten; a session that took money is a
        // settled payment, not an abandoned one, so it is left alone.
        var abandoned = invoice is { GatewayModule: { } previousModule, GatewayOrderId: { } previousOrderId }
            && !invoice.WasPaidThrough(previousModule)
                ? (Module: previousModule, OrderId: previousOrderId)
                : ((string Module, string OrderId)?)null;

        invoice.SetGatewaySession(cmd.Module, session.GatewayOrderId, now);

        // The session this attempt replaces will never store a method, so the consent recorded for it ends with it, in the same
        // save that points the invoice at the new session.
        if (abandoned is { } replaced)
        {
            await _consents.RevokeAsync(invoice.ClientId, replaced.Module, replaced.OrderId, ct);
        }

        // The consent is keyed by the gateway order and saved with the session, so it exists exactly when the gateway
        // registered the payment that will carry the method. The customer row the later charges need is saved in the same
        // save, so a payment that failed before this point leaves no customer behind.
        if (consenting is { } customer)
        {
            _context.CardChargeConsents.Add(
                CardChargeConsent.Grant(
                    customer.ClientId, cmd.Module, session.GatewayOrderId, now, cmd.ConsentTextVersion, cmd.IpAddress, cmd.UserAgent));
        }

        await _customers.SaveAsync(consenting, ct);

        // After the new session is saved, so a plugin that cannot cancel never costs the payer the new attempt.
        if (abandoned is { } old)
        {
            await _canceller.CancelAsync(old.Module, old.OrderId, invoice.Id, ct);
        }

        return Result.Success(new RedirectDto(session.RedirectUrl));
    }

    /// <summary>
    /// Settles everything a payment that saves its method needs before the gateway is asked: the module's plugin offers
    /// stored credentials, the caller is the invoice's own signed-in client and holds <c>ViewPayInvoices</c>, the account may
    /// pay by card, and the client's customer at the module is known (a new customer is staged for the caller's save).
    /// </summary>
    /// <param name="plugin">The payment plugin the payment is registered with; it must also offer stored credentials.</param>
    /// <param name="module">The payment plugin id.</param>
    /// <param name="invoice">The invoice being paid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The customer of the client who consents, or the code of the refusal: <c>UNAUTHORIZED</c> when the request carries no
    /// caller (checked first, so a guest is told to sign in whatever the module offers); <c>STORED_CREDENTIALS_NOT_SUPPORTED</c>; the caller's own failure when they act for no account;
    /// <c>INVOICE_NOT_FOUND</c> when the invoice is not theirs (not told apart from a missing one);
    /// <c>CLIENT_PERMISSION_DENIED</c> when a linked user lacks <c>ViewPayInvoices</c>; the card refusal of the account;
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when no customer can be built from the client; or
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin could not create the customer.
    /// </returns>
    private async Task<Result<EnsuredGatewayCustomer>> PrepareSavingAsync(
        IPaymentPlugin plugin, string module, Invoice invoice, CancellationToken ct)
    {
        // The order route is open to guests, who have no account to consent for: a refusal, not an exception.
        if (_caller.UserId is null)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(SharedErrorCodes.Unauthorized);
        }

        // The capability is read off the plugin the payment is registered with, not looked up a second time.
        if (plugin is not IStoredCredentialPlugin storedPlugin)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        }

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(clientId.ErrorCode!);
        }

        // Only the invoice's own client can consent to charges on their method; a user of another account is told the
        // invoice is not found, the way every other client route does.
        if (clientId.Value != invoice.ClientId)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // The route behind an order is open to guests, so the permission filter of the portal never guards it: a linked
        // user keeps a method for the account only where they may pay its invoices, as on the other stored-method routes.
        var mayPay = await _access.EnsureCallerHoldsAsync(ClientPermission.ViewPayInvoices, ct);
        if (mayPay.IsFailure)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(mayPay.ErrorCode!);
        }

        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(clientId.Value, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<EnsuredGatewayCustomer>.FailureCode(mayPayByCard.ErrorCode!);
        }

        try
        {
            var customer = await _customers.EnsureAsync(storedPlugin, clientId.Value, module, ct);
            return customer is null
                ? Result<EnsuredGatewayCustomer>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND)
                : Result.Success(customer);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The plugin's own exceptions cannot be told apart from an outage; the gateway's text stays in the log.
            _logger.LogWarning(
                ex, "Payment plugin {Module} could not create the customer of client {ClientId}.", module, clientId.Value);
            return Result<EnsuredGatewayCustomer>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }
    }

    /// <summary>
    /// Whether the invoice's existing session is still inside the gateway's live window and is
    /// not already known to be Declined. When the gateway's status API cannot be reached at all
    /// (e.g. a bank outage), the session's true state is unknowable: this is treated the same
    /// as "still live" rather than silently allowing a second session that could orphan a real
    /// payment at the gateway.
    /// </summary>
    /// <param name="invoice">The invoice being paid.</param>
    /// <param name="now">The moment of this request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a new session must be refused.</returns>
    private async Task<bool> HasLiveSessionAsync(Invoice invoice, DateTimeOffset now, CancellationToken ct)
    {
        if (!invoice.IsInsideGatewaySessionWindow(now))
        {
            return false;
        }

        var isDeclined = false;
        if (invoice.GatewayModule is not null && invoice.GatewayOrderId is not null)
        {
            var previousPlugin = await _pluginResolver.ResolveAsync(invoice.GatewayModule, ct);
            if (previousPlugin is not null)
            {
                try
                {
                    var status = await previousPlugin.GetStatusAsync(invoice.GatewayOrderId, ct);
                    isDeclined = status.State == GatewayPaymentState.Declined;
                }
                catch (OperationCanceledException)
                {
                    // The caller cancelled the request itself, not a gateway failure. Let it
                    // propagate rather than reinterpreting it as "session may still be live".
                    throw;
                }
                catch (Exception ex)
                {
                    // Cannot determine the previous session's state: refuse below rather than
                    // risk a second live session at the gateway. An operator needs to see why
                    // payers are being told to wait, so this is logged rather than swallowed.
                    _logger.LogWarning(
                        ex,
                        "Could not determine gateway status for invoice {InvoiceId} session {GatewayOrderId}; " +
                        "treating the existing session as still live.",
                        invoice.Id, invoice.GatewayOrderId);
                }
            }
        }

        return !isDeclined;
    }
}
