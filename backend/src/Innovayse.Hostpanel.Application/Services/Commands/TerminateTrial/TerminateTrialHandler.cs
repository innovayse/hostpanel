using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateService;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;

/// <summary>
/// Ends a running free trial: a trial awaiting payment has its unpaid conversion invoice cancelled through the
/// Billing module first, then the service is terminated by the terminate use case, which marks the trial
/// terminated and mails the client that it ended.
/// </summary>
/// <remarks>
/// <para>
/// The invoice is cancelled before the service is terminated, and a refusal to cancel it is answered with nothing
/// terminated, so the client is never left owing for a service that is gone. A paid invoice (also one partly refunded
/// since), or one whose payment is pending, refuses the termination: the payment completes the conversion, so a client
/// who paid just before the grace ended keeps the service. A refunded invoice, or one handed to collections, is left as
/// it is and the service is terminated, as for any other service. So is an open invoice that already took part of its
/// payment: Billing will not cancel it, and refusing would keep the trial running unpaid for ever, so the service is
/// terminated, the invoice is left for staff to settle (refund or collect the rest) and a warning is logged.
/// </para>
/// <para>
/// When the termination fails after the invoice was cancelled (the panel refuses), the service stays awaiting payment
/// without an invoice; <c>ConvertTrial</c> or the daily trial job recovers it (raising the invoice again, or
/// terminating). An immediate staff termination racing a client's upgrade can leave an unpaid conversion invoice on a
/// terminated service; that is accepted, as it is for an immediate cancellation of any service.
/// </para>
/// </remarks>
public sealed class TerminateTrialHandler
{
    /// <summary>The reason passed to the panel when the caller gives none.</summary>
    private const string DefaultReason = "Free trial terminated";

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads and cancels the conversion invoice through the Billing module, and terminates the service.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Logs a conversion invoice left open because it already took part of its payment.</summary>
    private readonly ILogger<TerminateTrialHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Reads and cancels the conversion invoice through the Billing module, and terminates the service.</param>
    /// <param name="logger">Logs a conversion invoice left open because it already took part of its payment.</param>
    public TerminateTrialHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<TerminateTrialHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The trial service and the reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The terminate use case's result, or <c>SERVICE_NOT_FOUND</c> when the id names no service,
    /// <c>SERVICE_NOT_IN_TRIAL</c> when its trial is not running, or the Billing module's refusal to cancel the
    /// conversion invoice.
    /// </returns>
    public async Task<Result> HandleAsync(TerminateTrialCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var service = await _context.ClientServices.AsNoTracking()
            .Where(s => s.Id == cmd.ServiceId)
            .Select(s => new { s.TrialStatus, s.NextRenewalAt })
            .FirstOrDefaultAsync(ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (service.TrialStatus is not (TrialStatus.Active or TrialStatus.AwaitingPayment))
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        }

        if (service is { TrialStatus: TrialStatus.AwaitingPayment, NextRenewalAt: { } start })
        {
            var cancelled = await CancelConversionInvoiceAsync(cmd.ServiceId, start, ct);
            if (cancelled.IsFailure)
            {
                return cancelled;
            }
        }

        return await _bus.InvokeAsync<Result>(new TerminateServiceCommand(cmd.ServiceId, cmd.Reason ?? DefaultReason), ct);
    }

    /// <summary>Cancels the conversion invoice while it is still unpaid.</summary>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="start">The start of the paid period the invoice bills.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also when there is no invoice, when it was refunded or handed to collections, and when it already took
    /// part of its payment and is left open for staff), <c>INVOICE_CANCEL_NOT_ALLOWED</c> when it is paid (also partly
    /// refunded) or its payment is pending, or the Billing module's other refusals.
    /// </returns>
    private async Task<Result> CancelConversionInvoiceAsync(int serviceId, DateTimeOffset start, CancellationToken ct)
    {
        var invoice = await _bus.GetServiceRenewalInvoiceAsync(serviceId, start, ct);
        if (invoice is { IsPaidOrPaymentPending: true })
        {
            // The client paid, or a payment is on its way and not applied yet: the trial is being converted, not ended.
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_CANCEL_NOT_ALLOWED);
        }

        if (invoice is not { Status: InvoiceStatus.Unpaid or InvoiceStatus.Overdue })
        {
            return Result.Success();
        }

        try
        {
            return await _bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoice.InvoiceId), ct);
        }
        catch (DomainRuleException ex) when (ex.Code == HostpanelErrorCodes.INVOICE_HAS_PAYMENTS)
        {
            // Part of the invoice is paid, so Billing keeps it; the trial still ends, and staff settle the invoice.
            _logger.LogWarning(
                "Trial service {ServiceId} is being terminated with its conversion invoice {InvoiceId} part paid; the invoice is left open for staff to settle.",
                serviceId, invoice.InvoiceId);
            return Result.Success();
        }
        catch (DomainRuleException ex)
        {
            // The invoice's own refusal (it was paid meanwhile) is answered as a code like the handler's other refusals,
            // so a caller on a queue gets a result instead of a dead letter.
            return Result.FailureCode(ex.Code);
        }
    }
}
