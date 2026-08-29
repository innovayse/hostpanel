namespace Innovayse.Application.Billing.Commands.StartGatewayPayment;

using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Handles <see cref="StartGatewayPaymentCommand"/>: resolves the payment plugin,
/// registers the payment (fresh per-attempt order number — the gateway rejects
/// reused ones), stores the session on the invoice, and returns the redirect URL.
/// </summary>
/// <param name="invoiceRepo">Invoice repository.</param>
/// <param name="clientRepo">Client repository, used to resolve the invoice's billing currency.</param>
/// <param name="pluginResolver">Payment plugin resolver.</param>
/// <param name="uow">Unit of work.</param>
/// <param name="billingOptions">Panel billing defaults, for the currency a client with none of their own is billed in.</param>
/// <param name="returnUrlOptions">The origins a payer may be handed back to, used to validate <c>ReturnUrl</c>.</param>
/// <param name="logger">Structured logger, used to record gateway status-probe failures during the live-session check.</param>
public sealed class StartGatewayPaymentHandler(
    IInvoiceRepository invoiceRepo,
    IClientRepository clientRepo,
    IPaymentPluginResolver pluginResolver,
    IUnitOfWork uow,
    IOptions<BillingOptions> billingOptions,
    IOptions<GatewayReturnUrlOptions> returnUrlOptions,
    ILogger<StartGatewayPaymentHandler> logger)
{
    /// <summary>
    /// How long a started gateway session is considered "live" (matches Inecobank's own
    /// hosted-page session lifetime). A second <c>start</c> for the same invoice inside this
    /// window is refused unless the existing session is already known to be Declined.
    /// </summary>
    private const int SessionWindowMinutes = 20;

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The start command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gateway redirect URL for the payer's browser.</returns>
    /// <exception cref="InvalidOperationException">
    /// Invoice not found/not payable, return URL not allowed, a live session is already in
    /// progress, plugin unavailable, or the invoice's currency does not match the plugin's.
    /// </exception>
    public async Task<string> HandleAsync(StartGatewayPaymentCommand cmd, CancellationToken ct)
    {
        var invoice = await invoiceRepo.FindByIdAsync(cmd.InvoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {cmd.InvoiceId} not found.");

        // Check payability before touching the bank at all — starting a session for a
        // Paid/Cancelled/Refunded invoice would register a live payable order at the gateway
        // for nothing, since SetGatewaySession() would reject it afterwards anyway.
        if (invoice.Status is not (InvoiceStatus.Unpaid or InvoiceStatus.Overdue))
        {
            throw new InvalidOperationException(
                $"Cannot start a gateway payment for invoice {invoice.Id}: status is {invoice.Status}.");
        }

        EnsureReturnUrlIsAllowed(cmd.ReturnUrl);

        var plugin = await pluginResolver.ResolveAsync(cmd.Module, ct)
            ?? throw new InvalidOperationException($"Payment method '{cmd.Module}' is not available.");

        await EnsureNoLiveSessionAsync(invoice, ct);

        await EnsureCurrencyMatchesAsync(invoice, plugin, ct);

        // Unique per attempt (gateway errorCode 1 on reuse); ≤ 25 chars fits int ids + unix seconds.
        var orderNumber = $"INV{invoice.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // The Application layer owns the major→minor conversion (ISO 4217 exponent-2).
        var amountMinor = CurrencyCodes.ToMinorUnits(invoice.Total);

        var session = await plugin.CreatePaymentAsync(
            new PaymentRequest(orderNumber, amountMinor, cmd.ReturnUrl, $"Invoice #{invoice.Id}", null),
            ct);

        invoice.SetGatewaySession(cmd.Module, session.GatewayOrderId);
        await uow.SaveChangesAsync(ct);

        return session.RedirectUrl;
    }

    /// <summary>
    /// Refuses to start a new session while the invoice's existing session is still inside the
    /// gateway's live window, unless that session is already known to be Declined. When the
    /// gateway's status API cannot be reached at all (e.g. a bank outage), the session's true
    /// state is unknowable — this is treated the same as "still live" rather than silently
    /// allowing a second session that could orphan a real payment at the gateway.
    /// </summary>
    /// <param name="invoice">The invoice being paid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when a live session already exists.</exception>
    private async Task EnsureNoLiveSessionAsync(Invoice invoice, CancellationToken ct)
    {
        if (invoice.GatewayStartedAt is not { } startedAt)
        {
            return;
        }

        var age = DateTimeOffset.UtcNow - startedAt;
        if (age >= TimeSpan.FromMinutes(SessionWindowMinutes))
        {
            return;
        }

        var isDeclined = false;
        if (invoice.GatewayModule is not null && invoice.GatewayOrderId is not null)
        {
            var previousPlugin = await pluginResolver.ResolveAsync(invoice.GatewayModule, ct);
            if (previousPlugin is not null)
            {
                try
                {
                    var status = await previousPlugin.GetStatusAsync(invoice.GatewayOrderId, ct);
                    isDeclined = status.State == GatewayPaymentState.Declined;
                }
                catch (OperationCanceledException)
                {
                    // The caller cancelled the request itself — not a gateway failure. Let it
                    // propagate rather than reinterpreting it as "session may still be live".
                    throw;
                }
                catch (Exception ex)
                {
                    // Cannot determine the previous session's state — refuse below rather than
                    // risk a second live session at the gateway. An operator needs to see why
                    // payers are being told to wait, so this is logged rather than swallowed.
                    logger.LogWarning(
                        ex,
                        "Could not determine gateway status for invoice {InvoiceId} session {GatewayOrderId}; " +
                        "treating the existing session as still live.",
                        invoice.Id, invoice.GatewayOrderId);
                }
            }
        }

        if (isDeclined)
        {
            return;
        }

        var minutesLeft = Math.Max(1, SessionWindowMinutes - (int)age.TotalMinutes);
        throw new InvalidOperationException(
            $"A payment attempt is already in progress for invoice {invoice.Id}; " +
            $"please wait {minutesLeft} minute(s) or complete the existing payment.");
    }

    /// <summary>
    /// Verifies the invoice's client bills in the same ISO 4217 currency the plugin will
    /// actually charge in, refusing before any money moves when they disagree or the client's
    /// currency has no known numeric mapping.
    /// </summary>
    /// <param name="invoice">The invoice being paid.</param>
    /// <param name="plugin">The resolved payment plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client's currency has no numeric mapping or does not match the plugin's currency.
    /// </exception>
    private async Task EnsureCurrencyMatchesAsync(Invoice invoice, IPaymentPlugin plugin, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(invoice.ClientId, ct);
        var defaultCurrency = billingOptions.Value.DefaultCurrency;
        var clientCurrency = client?.Currency ?? defaultCurrency;
        var clientCurrencyNumeric = CurrencyCodes.ToNumeric(clientCurrency)
            ?? throw new InvalidOperationException(
                $"Invoice {invoice.Id}: client currency '{clientCurrency}' has no known ISO 4217 numeric mapping.");

        if (!string.Equals(clientCurrencyNumeric, plugin.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Currency mismatch: invoice {invoice.Id} bills in '{clientCurrency}' (ISO 4217 {clientCurrencyNumeric}) " +
                $"but payment method resolves to currency ISO 4217 {plugin.CurrencyCode}.");
        }
    }

    /// <summary>
    /// Validates that <paramref name="returnUrl"/>'s origin is one of the panel's configured
    /// CORS origins, so a payer can never be handed off to an attacker-controlled domain.
    /// </summary>
    /// <param name="returnUrl">The absolute URL the payer will be redirected back to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the URL is not absolute or its origin is not an allowed CORS origin.
    /// </exception>
    private void EnsureReturnUrlIsAllowed(string returnUrl)
    {
        var allowedOrigins = returnUrlOptions.Value.AllowedOrigins;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Return URL '{returnUrl}' is not a valid absolute URL.");
        }

        var origin = $"{uri.Scheme}://{uri.Authority}";
        var isAllowed = allowedOrigins.Any(o =>
            string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            throw new InvalidOperationException($"Return URL origin '{origin}' is not an allowed origin.");
        }
    }
}
