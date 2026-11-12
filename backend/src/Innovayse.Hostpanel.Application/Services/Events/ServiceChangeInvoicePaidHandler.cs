using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Events;

/// <summary>
/// Applies an upgrade when its proration invoice is paid: dispatches <see cref="CompleteServiceChangeCommand"/> for the
/// approved request that waits for the invoice. A non-notification handler of <see cref="PaymentReceivedEvent"/>, and safe
/// as one: events are published after the save and dropped on failure, and the command is idempotent, so a request whose
/// change could not be applied (the hosting panel refused) stays Approved for staff to approve again. A payment that leaves the invoice
/// part paid is no failure and logs nothing: the request keeps waiting for the payment that settles it.
/// </summary>
public sealed class ServiceChangeInvoicePaidHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Dispatches the command.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a failure the staff approval recovers from.</summary>
    private readonly ILogger<ServiceChangeInvoicePaidHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Dispatches the command.</param>
    /// <param name="logger">Records a failure the staff approval recovers from.</param>
    public ServiceChangeInvoicePaidHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<ServiceChangeInvoicePaidHandler> logger)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Applies the plan change that waits for the paid invoice, if there is one.</summary>
    /// <param name="evt">The payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task HandleAsync(PaymentReceivedEvent evt, CancellationToken ct)
    {
        try
        {
            var requestId = await _context.ServiceChangeRequests
                .AsNoTracking()
                .Where(r => r.InvoiceId == evt.InvoiceId && r.Status == ServiceChangeStatus.Approved)
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(ct);
            if (requestId is null)
            {
                return;
            }

            var outcome = await _bus.InvokeAsync<Result>(new CompleteServiceChangeCommand(requestId.Value), ct);

            // A part payment is expected: the request keeps waiting, and the payment that settles the invoice applies it.
            if (outcome.IsFailure && outcome.ErrorCode != HostpanelErrorCodes.SERVICE_CHANGE_INVOICE_UNPAID)
            {
                _logger.LogWarning(
                    "Applying the plan change of paid invoice {InvoiceId} was refused with {ErrorCode}; staff can approve it again.",
                    evt.InvoiceId, outcome.ErrorCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Handlers of one event run on separate queues, so a throw here would not stop the client's payment-confirmation
            // mail, but it would only repeat the same failure through the retry cycle. The request stays Approved, which is
            // the record staff retry from, so the failure is logged for them instead.
            _logger.LogError(ex, "Applying the plan change of paid invoice {InvoiceId} failed; staff can approve it again.", evt.InvoiceId);
        }
    }
}
