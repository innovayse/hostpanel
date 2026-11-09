using System.Globalization;
using Hostpanel.Sdk.Plugins.Payment;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;

/// <summary>
/// Handles <see cref="ChargeInvoiceStoredMethodCommand"/>: charges what is still due on an invoice to the stored
/// method its client consented to, behind the per invoice claim gate.
/// </summary>
/// <remarks>
/// <para>
/// The host charges only a method it holds an active consent for under the module that stores it. The client's
/// default method is tried first, then the newest consent, and the first whose module is usable wins. Before the
/// plugin is called a claim with a fresh idempotency key is saved; the database allows one blocking claim per
/// invoice, so two runs for one invoice cannot both charge. A claim that is still unresolved, or held for an operator
/// because the gateway took money the invoice could not record, answers <c>STORED_CHARGE_ALREADY_IN_PROGRESS</c>: a
/// held invoice is never charged again.
/// </para>
/// <para>
/// The consent is read again after the claim is saved, before the plugin is called. A client who revoked it
/// in between is not charged: the claim is abandoned, because nothing was sent to the gateway. The invoice is read
/// again at the same point for the same reason: one paid by a run that finished meanwhile, or one whose due amount no
/// longer equals the claimed amount (a part payment landed), is not charged, and its claim is abandoned.
/// </para>
/// <para>
/// These rechecks narrow the window but do not close it. The consent and the invoice are read in separate statements
/// and neither is locked, so a revocation or a payment that commits after the recheck and before the plugin answers is
/// still charged; the claim gate only stops a second charge of the same invoice. A claim abandoned here never reached
/// the gateway and is not counted as a failed attempt by the renewal run
/// (<see cref="InvoicePaymentClaim.FailedAttemptFilter"/>).
/// </para>
/// <para>
/// A plugin that throws leaves the claim open: nothing is known about the money, so the claim is not released and the
/// reconciliation pass finds the charge by its key. Any answer the plugin does give goes to
/// <see cref="ApplyStoredChargeOutcomeCommand"/>, the one place an outcome is acted on.
/// </para>
/// <para>
/// With no capability, no consent or no stored method this refuses and writes nothing: the invoice stays unpaid and the
/// ordinary invoice and pay link flow runs.
/// </para>
/// </remarks>
public sealed class ChargeInvoiceStoredMethodHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the stored credential capability of a payment gateway module.</summary>
    private readonly IStoredCredentialPluginResolver _plugins;

    /// <summary>The Wolverine bus, for the Clients module's status checks and the outcome command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The configured origins; the first one is where an authenticating payer is sent back to.</summary>
    private readonly IOptions<CorsOptions> _origins;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ChargeInvoiceStoredMethodHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="plugins">Resolves the stored credential capability of a payment gateway module.</param>
    /// <param name="bus">The Wolverine bus, for the Clients module's status checks and the outcome command.</param>
    /// <param name="origins">The configured origins; the first one is where an authenticating payer is sent back to.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public ChargeInvoiceStoredMethodHandler(
        IHostpanelDbContext context,
        IStoredCredentialPluginResolver plugins,
        IMessageBus bus,
        IOptions<CorsOptions> origins,
        IClock clock,
        ILogger<ChargeInvoiceStoredMethodHandler> logger)
    {
        _context = context;
        _plugins = plugins;
        _bus = bus;
        _origins = origins;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The invoice to charge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Where the charge stands once the plugin answered (settled, declined, awaiting the payer or pending);
    /// <c>INVOICE_NOT_FOUND</c>; <c>INVOICE_NOT_PAYABLE</c> when nothing is due or the invoice is not open;
    /// <c>CLIENT_SUSPENDED</c> / <c>CLIENT_CLOSED</c> / <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not be
    /// charged; <c>STORED_CHARGE_ALREADY_IN_PROGRESS</c> when the invoice already has an unresolved or held claim;
    /// <c>CARD_CONSENT_REQUIRED</c> when the client holds no active consent, or revoked it while the claim was written; <c>STORED_CREDENTIALS_NOT_SUPPORTED</c>
    /// when no consented module is usable; <c>NO_SAVED_PAYMENT_METHODS</c> when the client has no customer at the
    /// module; <c>CURRENCY_NOT_FOUND</c> / <c>GATEWAY_CURRENCY_MISMATCH</c> when the module cannot take the invoice's
    /// currency; or <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin did not answer (the claim stays open
    /// and the reconciliation pass resolves it).
    /// </returns>
    public async Task<Result<InvoiceChargeDto>> HandleAsync(ChargeInvoiceStoredMethodCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Payments are loaded because only what is still due is charged.
        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsPayable || invoice.AmountDue <= 0m)
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        var mayTransact = await _bus.EnsureClientMayTransactAsync(invoice.ClientId, ct);
        if (mayTransact.IsFailure)
        {
            return Result<InvoiceChargeDto>.FailureCode(mayTransact.ErrorCode!);
        }

        var mayPayByCard = await _bus.EnsureClientMayPayByCardAsync(invoice.ClientId, ct);
        if (mayPayByCard.IsFailure)
        {
            return Result<InvoiceChargeDto>.FailureCode(mayPayByCard.ErrorCode!);
        }

        // The gate: an invoice with a charge in flight, or one held for an operator, is never charged a second time.
        if (await _context.InvoicePaymentClaims
            .Where(c => c.InvoiceId == invoice.Id)
            .Where(InvoicePaymentClaim.BlockingFilter)
            .AnyAsync(ct))
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        }

        var consents = await _context.CardChargeConsents
            .AsNoTracking()
            .Where(c => c.ClientId == invoice.ClientId && c.MethodRef != null && c.RevokedAt == null)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.GrantedAt)
            .ToListAsync(ct);
        if (consents.Count == 0)
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        }

        var currency = await _context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == invoice.Currency, ct);
        if (currency is null)
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        }

        var target = await PickTargetAsync(consents, ct);
        if (target.Plugin is null)
        {
            return Result<InvoiceChargeDto>.FailureCode(
                target.Seen ? HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS : HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        }

        // The capability sits on the plugin's payment class (that is how a plugin opts in), and the capability asks
        // the host to check the currency before it charges.
        if (target.Plugin is IPaymentPlugin payment && !payment.AcceptsCurrency(currency.Numeric))
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.GATEWAY_CURRENCY_MISMATCH);
        }

        var amountMinor = CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency);
        var claim = InvoicePaymentClaim.Open(
            invoice.Id, invoice.ClientId, target.Consent!.GatewayModuleId, target.Consent.MethodRef!, NewKey(invoice.Id),
            amountMinor, invoice.Currency, _clock.UtcNow);
        _context.InvoicePaymentClaims.Add(claim);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A run that started at the same moment claimed the invoice first.
            _context.DetachAll();
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        }

        // The client may have revoked the consent while the claim was being written. Nothing has been sent to the
        // gateway yet, so the claim is ended as never charged and the method is left alone.
        var consentId = target.Consent.Id;
        var stillConsented = await _context.CardChargeConsents
            .AsNoTracking()
            .AnyAsync(c => c.Id == consentId && c.MethodRef != null && c.RevokedAt == null, ct);
        if (!stillConsented)
        {
            claim.MarkAbandoned(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        }

        // The invoice was read before the claim was taken. A run that finished in between (it held no claim yet when this
        // one looked) may have paid it, so it is read again now that the claim holds the invoice: nothing is sent to the
        // gateway for an invoice that owes nothing, and the claim is ended as never charged. A part payment that landed
        // meanwhile changes what is due, and the claim and the plugin call carry the amount of the earlier read, so a
        // due amount that no longer equals the claimed one ends the claim the same way rather than overcharge the client.
        var current = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == invoice.Id, ct);
        if (current is null
            || !current.IsPayable
            || current.AmountDue <= 0m
            || CurrencyCode.ToMinorUnits(current.AmountDue, current.Currency) != claim.AmountMinor)
        {
            claim.MarkAbandoned(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        }

        StoredChargeResult charged;
        try
        {
            charged = await target.Plugin.ChargeAsync(
                new StoredChargeInstruction(
                    target.CustomerRef!, claim.MethodRef, amountMinor, currency.Numeric,
                    invoice.Id.ToString(CultureInfo.InvariantCulture), claim.IdempotencyKey, ReturnUrl(invoice.Id)),
                ct);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The outcome is unknown: the money may or may not have been taken. The claim stays open on purpose, so
            // the invoice is not charged again and the reconciliation pass finds the charge by its key.
            _logger.LogWarning(
                ex, "Payment plugin {Module} gave no answer to the stored charge of invoice {InvoiceId}; claim {ClaimId} stays open.",
                claim.GatewayModuleId, invoice.Id, claim.Id);
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        }

        return await _bus.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(
                claim.Id, charged.Outcome, charged.TransactionId, charged.AmountMinor, charged.Currency, charged.ActionUrl),
            ct);
    }

    /// <summary>
    /// Picks the first consent (the default one first, then the newest) whose module is usable and has a customer for
    /// the client, and resolves its capability.
    /// </summary>
    /// <param name="consents">The client's active consents, the default one first and then newest first.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The chosen consent, its capability and the client's customer reference; or no capability, with
    /// <c>Seen</c> telling whether any consented module was usable but had no customer for the client.
    /// </returns>
    private async Task<StoredChargeTarget> PickTargetAsync(IReadOnlyList<CardChargeConsent> consents, CancellationToken ct)
    {
        var seen = false;
        foreach (var consent in consents)
        {
            var plugin = await _plugins.ResolveAsync(consent.GatewayModuleId, ct);
            if (plugin is null)
            {
                continue;
            }

            seen = true;
            var customerRef = await _context.GatewayCustomers
                .AsNoTracking()
                .Where(c => c.ClientId == consent.ClientId && c.GatewayModuleId == consent.GatewayModuleId)
                .Select(c => c.CustomerRef)
                .FirstOrDefaultAsync(ct);
            if (customerRef is not null)
            {
                return new StoredChargeTarget(consent, plugin, customerRef, true);
            }
        }

        return new StoredChargeTarget(null, null, null, seen);
    }

    /// <summary>Makes the idempotency key of a new attempt: opaque ASCII, well inside the 64 characters the capability allows.</summary>
    /// <param name="invoiceId">The invoice charged.</param>
    /// <returns>A key no other attempt has.</returns>
    private static string NewKey(int invoiceId) =>
        string.Create(CultureInfo.InvariantCulture, $"inv{invoiceId}-{Guid.NewGuid():N}");

    /// <summary>The address an authenticating payer comes back to: the invoice page on the first configured origin.</summary>
    /// <param name="invoiceId">The invoice charged.</param>
    /// <returns>An absolute address, or <see langword="null"/> when the deployment configures no origin.</returns>
    private string? ReturnUrl(int invoiceId)
    {
        var origin = _origins.Value.Origins.FirstOrDefault();
        return origin is null ? null : string.Create(CultureInfo.InvariantCulture, $"{origin.TrimEnd('/')}/client/invoices/{invoiceId}");
    }
}
