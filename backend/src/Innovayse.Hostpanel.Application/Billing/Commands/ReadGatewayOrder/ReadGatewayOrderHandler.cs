using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;

/// <summary>
/// Handles <see cref="ReadGatewayOrderCommand"/>: the read a payment plugin's callback asked for. Nothing here decides a
/// payment. An order that is not settled yet goes through <see cref="CompleteGatewayPaymentCommand"/>, the one settlement
/// path of the return page, the completion routes and the reconciliation job, so the amount, currency and reference checks,
/// the credit surplus, the saved method and the fulfilment stay in one place and a repeat settles once. An invoice that
/// was already settled through the plugin is only watched for a reversal.
/// </summary>
/// <remarks>
/// A status read never moves money. A payment the gateway later reports as no longer paid (the contract folds a refund, a
/// reversal and a charge-back into <see cref="GatewayPaymentState.Declined"/>) changes nothing on the invoice or the ledger:
/// the host cannot tell a partial return from a full one, nor one it made itself from one made at the gateway. Staff are
/// told once per session through <see cref="GatewayPaymentReversedEvent"/>, and only when the invoice shows no refund
/// through that gateway (<see cref="Invoice.ShowsRefundThrough"/>).
/// </remarks>
public sealed class ReadGatewayOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the payment plugin that is asked for the status of a settled order.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>Wolverine message bus, which runs the settlement and publishes the staff alert.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the moment of the alert is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ReadGatewayOrderHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Resolves the payment plugin that is asked for the status of a settled order.</param>
    /// <param name="bus">Wolverine message bus.</param>
    /// <param name="clock">The clock the moment of the alert is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public ReadGatewayOrderHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        IMessageBus bus,
        IClock clock,
        ILogger<ReadGatewayOrderHandler> logger)
    {
        _context = context;
        _pluginResolver = pluginResolver;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The read command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the order is unknown, belongs to another plugin, was replaced by a newer attempt, or was read (whatever
    /// the gateway answered); otherwise the failure the settlement path gave, for the caller's log.
    /// </returns>
    public async Task<Result> HandleAsync(ReadGatewayOrderCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Owned by the plugin that asked, by the binding's id: an order of another plugin, an unknown order and a session
        // that a newer attempt replaced all find nothing, and none of them is told apart to the sender. Payments are loaded
        // because the settled branch asks the invoice what was paid and refunded through the plugin.
        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .Where(i => i.GatewayModule == cmd.PluginId && i.GatewayOrderId == cmd.GatewayOrderId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);
        if (invoice is null)
        {
            _logger.LogDebug("Plugin {PluginId} asked for a gateway order that no invoice holds; ignored.", cmd.PluginId);
            return Result.Success();
        }

        if (invoice.IsSettled && invoice.WasPaidThrough(cmd.PluginId))
        {
            await NotifyReversalAsync(invoice, cmd.PluginId, ct);
            return Result.Success();
        }

        var completed = await _bus.InvokeAsync<Result<GatewayCompletionDto>>(new CompleteGatewayPaymentCommand(invoice.Id), ct);
        return completed.IsSuccess ? Result.Success() : Result.FailureCode(completed.ErrorCode!);
    }

    /// <summary>
    /// Asks the gateway once whether an invoice settled through it still shows as paid and, when the answer is that it does
    /// not and the invoice explains it by no refund of its own, tells staff once per session. The mark is saved first and
    /// the alert published after, like the other gateway alerts: a failed save repeats the step on the next read, and once
    /// saved a replay finds the mark and sends nothing. A crash between the save and the publish loses the alert; it is
    /// never sent a second time. Nothing but the mark is written.
    /// </summary>
    /// <param name="invoice">The invoice settled through the plugin (tracked, payments loaded).</param>
    /// <param name="module">The plugin id of the session that took the money.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the mark is saved and the alert published, or the read found nothing to report.</returns>
    private async Task NotifyReversalAsync(Invoice invoice, string module, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Both are known without the gateway, so a told session or the host's own refund never costs a call to it.
        if (invoice.GatewayReversalNotifiedAt is not null || invoice.ShowsRefundThrough(module, now))
        {
            return;
        }

        var plugin = await _pluginResolver.ResolveAsync(module, ct);
        if (plugin is null || invoice.GatewayOrderId is null)
        {
            return;
        }

        var status = await plugin.GetStatusAsync(invoice.GatewayOrderId, ct);
        if (status.State != GatewayPaymentState.Declined)
        {
            return;
        }

        _logger.LogWarning(
            "Invoice {InvoiceId} was settled through {Module}, but the gateway now reports the payment as {State} ({Raw}); staff are told, nothing is changed.",
            invoice.Id, module, status.State, status.RawStatus);

        invoice.MarkGatewayReversalNotified(now);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Another writer changed the invoice first (a refund being recorded, most likely). The mark was not saved, so the
            // next read of this order looks again.
            _context.DetachAll();
            _logger.LogInformation(
                "Invoice {InvoiceId} changed while staff were being told about a reversed payment; the next read looks again.",
                invoice.Id);
            return;
        }

        await _bus.PublishAsync(new GatewayPaymentReversedEvent(invoice.Id, invoice.ClientId, module)
        {
            OccurredAt = now,
        });
    }
}
