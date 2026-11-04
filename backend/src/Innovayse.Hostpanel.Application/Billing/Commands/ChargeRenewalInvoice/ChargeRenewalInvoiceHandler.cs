using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
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
/// method without the client, when every condition for that holds, and tells the client when it did not work.
/// </summary>
/// <remarks>
/// Money moves here, so every condition is checked before the gateway is touched: the invoice is an open renewal
/// that still owes something, the account may pay by card, the client has an account at a gateway module that offers
/// saved methods with a default method, the client agreed to automatic charges for exactly that method at that
/// module, no attempt was already made for this invoice, and nobody else is charging it at this moment
/// (<see cref="InvoicePaymentClaimGate"/>). The agreement and the amount due are read again after the claim is taken,
/// because they were read before others could change them. The charge carries the claim's key, so a retry cannot take
/// the money twice, and a charge whose answer was lost is found again and settled before any new attempt. An
/// invoice that is not charged stays exactly as it was: unpaid, for the client to pay.
/// </remarks>
public sealed class ChargeRenewalInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Hands out the exclusive right to charge the invoice.</summary>
    private readonly InvoicePaymentClaimGate _claims;

    /// <summary>Charges the method and settles or ends the claim.</summary>
    private readonly InvoiceCardCharger _charger;

    /// <summary>The client portal's address, for the pay link in the e-mail.</summary>
    private readonly IOptions<ClientPortalOptions> _clientPortal;

    /// <summary>Message bus, for the account checks and the e-mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock claims are ended on.</summary>
    private readonly IClock _clock;

    /// <summary>Logs why an invoice is not charged.</summary>
    private readonly ILogger<ChargeRenewalInvoiceHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="claims">Hands out the exclusive right to charge the invoice.</param>
    /// <param name="charger">Charges the method and settles or ends the claim.</param>
    /// <param name="clientPortal">The client portal's address, for the pay link in the e-mail.</param>
    /// <param name="bus">Message bus, for the account checks and the e-mail.</param>
    /// <param name="clock">The clock claims are ended on.</param>
    /// <param name="logger">Logs why an invoice is not charged.</param>
    public ChargeRenewalInvoiceHandler(
        IHostpanelDbContext context,
        StoredCredentialGateway gateway,
        InvoicePaymentClaimGate claims,
        InvoiceCardCharger charger,
        IOptions<ClientPortalOptions> clientPortal,
        IMessageBus bus,
        IClock clock,
        ILogger<ChargeRenewalInvoiceHandler> logger)
    {
        _context = context;
        _gateway = gateway;
        _claims = claims;
        _charger = charger;
        _clientPortal = clientPortal;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Charges the invoice when it may be, and records how that went.</summary>
    /// <param name="cmd">The renewal invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// How the charge ended; <c>INVOICE_NOT_FOUND</c> when the id names no invoice; or the error code the payment
    /// could not be recorded with (the attempt stays held and is reconciled later).
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
        if (!invoice.IsPayable || invoice.AmountDue <= 0m || !invoice.Items.Any(i => i.IsRenewal))
        {
            return Skipped(invoice.Id, "the invoice is not an open renewal that owes money");
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

        var target = await FindTargetAsync(invoice.ClientId, ct);
        if (target.Unavailable)
        {
            return Result.Success(RenewalChargeOutcome.Unavailable);
        }

        if (target.ModuleId is null || target.CustomerRef is null || target.MethodRef is null)
        {
            return Skipped(invoice.Id, "the client has no default saved method they agreed to have charged automatically");
        }

        var key = InvoicePaymentClaim.AutomaticKey(invoice.Id, Guid.NewGuid());
        var acquired = await _claims.AcquireAsync(invoice, target.ModuleId, InvoicePaymentClaimKind.Automatic, key, target.CustomerRef, ct);
        switch (acquired.Status)
        {
            case InvoicePaymentClaimAcquisition.Busy:
                return Result.Success(RenewalChargeOutcome.Busy);
            case InvoicePaymentClaimAcquisition.AlreadySettled:
                return Result.Success(RenewalChargeOutcome.Charged);
        }

        var claim = acquired.Claim!;

        // The agreement and the amount were read before the claim was taken. Another request may have withdrawn the
        // agreement, paid the invoice or changed what is owed since, so both are read again before any money moves.
        var current = await _context.Invoices.AsNoTracking().Include(i => i.Transactions).FirstAsync(i => i.Id == invoice.Id, ct);
        var stillAgreed = await _context.CardChargeConsents.AsNoTracking().AnyAsync(
            c => c.ClientId == invoice.ClientId && c.GatewayModuleId == target.ModuleId && c.GatewayMethodRef == target.MethodRef
                 && c.Status == CardConsentStatus.Active, ct);
        if (!stillAgreed || !current.IsPayable || current.AmountDue <= 0m)
        {
            claim.MarkExpired(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
            return Skipped(invoice.Id, "the agreement or the amount due changed while the claim was being taken");
        }

        var payUrl = $"{_clientPortal.Value.BaseUrl.TrimEnd('/')}/client/invoices/{invoice.Id.ToString(CultureInfo.InvariantCulture)}/pay";
        var charge = await _charger.ChargeAsync(current, claim, target.CustomerRef, target.MethodRef, payUrl, ct);
        switch (charge.Outcome)
        {
            case InvoiceChargeOutcome.Settled:
                return Result.Success(RenewalChargeOutcome.Charged);
            case InvoiceChargeOutcome.Declined:
                await NotifyAsync(invoice, payUrl, authenticationRequired: false, ct);
                return Result.Success(RenewalChargeOutcome.Declined);
            case InvoiceChargeOutcome.ActionRequired:
                await NotifyAsync(invoice, payUrl, authenticationRequired: true, ct);
                return Result.Success(RenewalChargeOutcome.NeedsAuthentication);
            case InvoiceChargeOutcome.Pending:
                return Result.Success(RenewalChargeOutcome.Busy);
            case InvoiceChargeOutcome.NotRecorded:
                return Result<RenewalChargeOutcome>.FailureCode(charge.ErrorCode ?? HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
            default:
                return Result.Success(RenewalChargeOutcome.Unavailable);
        }
    }

    /// <summary>Finds the saved method to charge: the default one of a module the client agreed to have charged.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The module, the client's account and the method; empty fields when there is none; <c>Unavailable</c> when a gateway could not be asked.</returns>
    private async Task<ChargeTarget> FindTargetAsync(int clientId, CancellationToken ct)
    {
        // The client's agreement is for exactly one method at one module: a method saved some other way is never
        // charged by itself.
        var agreed = await _context.CardChargeConsents
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.Status == CardConsentStatus.Active)
            .Select(c => new { c.GatewayModuleId, c.GatewayMethodRef })
            .ToListAsync(ct);

        foreach (var moduleId in agreed.Select(a => a.GatewayModuleId).Distinct())
        {
            var customerRef = await _context.GatewayCustomers
                .AsNoTracking()
                .Where(g => g.ClientId == clientId && g.GatewayModuleId == moduleId)
                .Select(g => g.CustomerRef)
                .FirstOrDefaultAsync(ct);
            if (customerRef is null)
            {
                continue;
            }

            var methods = await _gateway.ListMethodsAsync(moduleId, customerRef, ct);
            if (methods.ErrorCode == HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE)
            {
                // The module was switched off or no longer offers saved methods: nothing can be charged through it.
                continue;
            }

            if (methods.IsFailure)
            {
                _logger.LogWarning("Could not list the saved methods of client {ClientId} at module {Module}: {Code}", clientId, moduleId, methods.ErrorCode);
                return new ChargeTarget(null, null, null, true);
            }

            var method = methods.Value!.FirstOrDefault(m => m.IsDefault);
            if (method is not null && agreed.Any(a => a.GatewayModuleId == moduleId && a.GatewayMethodRef == method.MethodRef))
            {
                return new ChargeTarget(moduleId, customerRef, method.MethodRef, false);
            }
        }

        return new ChargeTarget(null, null, null, false);
    }

    /// <summary>Tells the client the automatic charge did not go through, with a link to pay the invoice.</summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="payUrl">The portal page where the invoice is paid.</param>
    /// <param name="authenticationRequired">Whether the payer has to authenticate, as opposed to a plain decline.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task NotifyAsync(Invoice invoice, string payUrl, bool authenticationRequired, CancellationToken ct)
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
            payUrl));
        var slug = authenticationRequired ? RenewalTemplatesSeeder.ChargeAuthenticationSlug : RenewalTemplatesSeeder.ChargeDeclinedSlug;
        await _bus.InvokeAsync(new SendEmailCommand(email, client.Language, slug, data, new ContactCopy(invoice.ClientId, ContactNotification.Invoice)), ct);
    }

    /// <summary>Logs why the invoice is not charged and answers that nothing happened.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="reason">Why it is not charged.</param>
    /// <returns>A successful <see cref="RenewalChargeOutcome.Skipped"/>.</returns>
    private Result<RenewalChargeOutcome> Skipped(int invoiceId, string reason)
    {
        _logger.LogInformation("Invoice {InvoiceId} is not charged automatically: {Reason}.", invoiceId, reason);
        return Result.Success(RenewalChargeOutcome.Skipped);
    }

    /// <summary>The saved method an invoice would be charged to.</summary>
    /// <param name="ModuleId">The payment gateway module, or <see langword="null"/> when there is none.</param>
    /// <param name="CustomerRef">The client's account at the module.</param>
    /// <param name="MethodRef">The default saved method the client agreed to have charged.</param>
    /// <param name="Unavailable">Whether a gateway could not be asked.</param>
    private sealed record ChargeTarget(string? ModuleId, string? CustomerRef, string? MethodRef, bool Unavailable);
}
