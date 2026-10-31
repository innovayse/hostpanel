using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeRenewalInvoice;

/// <summary>
/// Handles <see cref="ChargeRenewalInvoiceCommand"/>: takes a renewal invoice's money from the client's saved
/// card without the client, when every condition for that holds, and tells the client when it did not work.
/// </summary>
/// <remarks>
/// Money moves here, so every condition is checked before the card is touched: the invoice is an open renewal
/// that still owes something, the account may pay by card, the client has a customer with a default card, the
/// client agreed to automatic charges for exactly that card, no attempt was already made for this invoice, and
/// nobody else is charging it at this moment (<see cref="InvoicePaymentClaimGate"/>). The charge carries the claim's
/// key, so a retry cannot take the money twice, and a charge whose answer was lost is found again and settled
/// before any new attempt. An invoice that is not charged stays exactly as it was: unpaid, for the client to pay.
/// </remarks>
public sealed class ChargeRenewalInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Card payment gateway, for the saved cards and the charge.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Hands out the exclusive right to charge the invoice.</summary>
    private readonly InvoicePaymentClaimGate _claims;

    /// <summary>The client portal's base URL, for the pay link in the mail.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Message bus, for the client's account, the settlement and the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ChargeRenewalInvoiceHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">Card payment gateway, for the saved cards and the charge.</param>
    /// <param name="claims">Hands out the exclusive right to charge the invoice.</param>
    /// <param name="clientPortal">The client portal's base URL, for the pay link in the mail.</param>
    /// <param name="bus">Message bus, for the client's account, the settlement and the mail.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public ChargeRenewalInvoiceHandler(
        IHostpanelDbContext context,
        ICardPaymentGateway cardGateway,
        InvoicePaymentClaimGate claims,
        IOptions<ClientPortalOptions> clientPortal,
        IMessageBus bus,
        IClock clock,
        ILogger<ChargeRenewalInvoiceHandler> logger)
    {
        _context = context;
        _cardGateway = cardGateway;
        _claims = claims;
        _clientPortal = clientPortal;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Charges the invoice when it may be, and records how that went.</summary>
    /// <param name="cmd">The renewal invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// How the charge ended; <c>INVOICE_NOT_FOUND</c> when the id names no invoice;
    /// <c>GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED</c> when the gateway reported success without a
    /// transaction id (the attempt stays held and is reconciled later); or whatever the settlement refused with,
    /// once it has handed the money back.
    /// </returns>
    public async Task<Result<RenewalChargeOutcome>> HandleAsync(ChargeRenewalInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<RenewalChargeOutcome>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Only an open renewal invoice that still owes something is charged, and never twice for the same attempt.
        if (!invoice.IsPayable || invoice.AmountDue <= 0m || !invoice.Items.Any(i => i.IsRenewal) || !_cardGateway.IsConfigured)
        {
            return Skipped(invoice.Id, "the invoice is not an open renewal that owes money, or no card gateway is configured");
        }

        if (await _context.InvoicePaymentClaims.AnyAsync(
                c => c.InvoiceId == invoice.Id && c.Kind == InvoicePaymentClaimKind.Automatic
                     && (c.Outcome == InvoicePaymentClaimOutcome.Failed || c.Outcome == InvoicePaymentClaimOutcome.Settled), ct))
        {
            return Skipped(invoice.Id, "an automatic attempt already ended for it");
        }

        var mayTransact = await _bus.EnsureClientMayTransactAsync(invoice.ClientId, ct);
        var mayPayByCard = mayTransact.IsSuccess ? await _bus.EnsureClientMayPayByCardAsync(invoice.ClientId, ct) : mayTransact;
        if (mayPayByCard.IsFailure)
        {
            return Skipped(invoice.Id, $"the account may not pay by card ({mayPayByCard.ErrorCode})");
        }

        var customer = await _bus.InvokeAsync<Result<CardGatewayCustomerDto>>(new GetCardGatewayCustomerQuery(invoice.ClientId), ct);
        if (customer.IsFailure || customer.Value!.CustomerId is not { } customerId)
        {
            return Skipped(invoice.Id, "the client has no card gateway customer");
        }

        var methods = await _cardGateway.ListPaymentMethodsAsync(new CardCustomerInstruction(customerId), ct);
        if (methods.Failure is { } listFailure)
        {
            _logger.LogWarning("Could not list the saved cards of invoice {InvoiceId}'s client: {Failure}", invoice.Id, listFailure);
            return Result.Success(listFailure == CardGatewayFailure.Unavailable ? RenewalChargeOutcome.Unavailable : RenewalChargeOutcome.Skipped);
        }

        var method = methods.Methods.FirstOrDefault(m => m.IsDefault && m.Type == SavedPaymentMethodType.Card);
        if (method is null)
        {
            return Skipped(invoice.Id, "the client has no default card");
        }

        // The client's agreement is for exactly this card: a card saved some other way is never charged by itself.
        if (await _context.FindActiveCardConsentAsync(invoice.ClientId, method.Id, ct) is null)
        {
            return Skipped(invoice.Id, "the client has not agreed to automatic charges for the default card");
        }

        var key = InvoicePaymentClaim.AutomaticKey(invoice.Id, Guid.NewGuid());
        var acquired = await _claims.AcquireAsync(invoice, InvoicePaymentClaimKind.Automatic, key, customerId, ct);
        switch (acquired.Status)
        {
            case InvoicePaymentClaimAcquisition.Busy:
                return Result.Success(RenewalChargeOutcome.Busy);
            case InvoicePaymentClaimAcquisition.AlreadySettled:
                return Result.Success(RenewalChargeOutcome.Charged);
        }

        var claim = acquired.Claim!;
        var amount = invoice.AmountDue;
        var charge = await _cardGateway.ChargeSavedPaymentMethodAsync(
            new CardChargeInstruction(customerId, method.Id, amount, invoice.Currency, invoice.Id, claim.GatewayIdempotencyKey), ct);

        if (charge.Failure is CardGatewayFailure.Unavailable)
        {
            // Nothing is known about the money: the claim stays held and is reconciled with the gateway before any new attempt.
            _logger.LogWarning("The card gateway could not be reached while charging invoice {InvoiceId}.", invoice.Id);
            return Result.Success(RenewalChargeOutcome.Unavailable);
        }

        if (charge.Success)
        {
            return await SettleAsync(invoice, claim, charge, amount, ct);
        }

        var authenticationRequired = charge.Failure is CardGatewayFailure.AuthenticationRequired;
        claim.MarkFailed(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        await NotifyAsync(invoice, authenticationRequired, ct);
        return Result.Success(authenticationRequired ? RenewalChargeOutcome.NeedsAuthentication : RenewalChargeOutcome.Declined);
    }

    /// <summary>Records a charge the gateway took: settles the invoice and ends the claim.</summary>
    /// <param name="invoice">The invoice charged.</param>
    /// <param name="claim">The held claim.</param>
    /// <param name="charge">What the gateway reported.</param>
    /// <param name="amount">The amount charged.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The charged outcome, or the settlement's refusal.</returns>
    private async Task<Result<RenewalChargeOutcome>> SettleAsync(
        Invoice invoice, InvoicePaymentClaim claim, CardChargeResult charge, decimal amount, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(charge.TransactionId))
        {
            // The money may be taken but there is nothing to record it under: the claim stays held and the next
            // attempt finds the charge by its key.
            return Result<RenewalChargeOutcome>.FailureCode(HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        }

        var settled = await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(invoice.Id, charge.TransactionId, amount, invoice.Currency), ct);
        if (settled.IsFailure)
        {
            claim.MarkFailed(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
            return Result<RenewalChargeOutcome>.FailureCode(settled.ErrorCode!);
        }

        claim.MarkSettled(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return Result.Success(RenewalChargeOutcome.Charged);
    }

    /// <summary>Mails the client that the automatic charge did not go through, with the link to pay the invoice by hand.</summary>
    /// <param name="invoice">The invoice that stays unpaid.</param>
    /// <param name="authenticationRequired">Whether the bank asked the cardholder to authenticate, rather than declining.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the mail is handed over, or when nobody is to be mailed.</returns>
    private async Task NotifyAsync(Invoice invoice, bool authenticationRequired, CancellationToken ct)
    {
        var client = await _bus.GetRequiredClientBriefAsync(invoice.ClientId, withEmail: true, ct);

        // Money owed is an invoice mail, so the client's invoice switch decides, as for a renewal reminder.
        if (client is not { NotifyInvoice: true, Email: { } email })
        {
            return;
        }

        await RenewalTemplatesSeeder.EnsureSeededAsync(_context, ct);
        var data = new RenewalChargeFailedTemplateModel(new RenewalInvoiceTemplateData(
            invoice.Id,
            invoice.Total,
            invoice.Currency,
            invoice.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/invoices/{invoice.Id}/pay"));
        var slug = authenticationRequired ? RenewalTemplatesSeeder.ChargeAuthenticationSlug : RenewalTemplatesSeeder.ChargeDeclinedSlug;
        await _bus.InvokeAsync(new SendEmailCommand(email, client.Language, slug, data, new ContactCopy(invoice.ClientId, ContactNotification.Invoice)), ct);
    }

    /// <summary>Answers that nothing was charged because the invoice or the account is not eligible, and says why in the log.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="reason">Why it was left alone.</param>
    /// <returns>The skipped outcome.</returns>
    private Result<RenewalChargeOutcome> Skipped(int invoiceId, string reason)
    {
        _logger.LogInformation("Invoice {InvoiceId} is not charged automatically: {Reason}.", invoiceId, reason);
        return Result.Success(RenewalChargeOutcome.Skipped);
    }
}
