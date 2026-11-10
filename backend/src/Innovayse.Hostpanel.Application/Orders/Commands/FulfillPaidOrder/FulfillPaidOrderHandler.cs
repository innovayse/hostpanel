using System.Runtime.ExceptionServices;
using Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceLineShares;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineRefunded;
using Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Application.Orders.Extensions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Commands.FulfillPaidOrder;

/// <summary>
/// Handles <see cref="FulfillPaidOrderCommand"/>: accepts the paid order, fulfils its lines through
/// <see cref="FulfillOrderLinesCommand"/>, and refunds a domain line's own amount through
/// <see cref="RefundInvoicePaymentCommand"/> when the registrar rejects it.
/// </summary>
public sealed class FulfillPaidOrderHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<FulfillPaidOrderHandler> _logger;

    /// <summary>The clock that dates the acceptance and a failed registration.</summary>
    private readonly IClock _clock;

    /// <summary>Gives a failed line's credit share back to the client's credit balance.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine message bus.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock that dates the acceptance and a failed registration.</param>
    /// <param name="clientCredit">Gives a failed line's credit share back to the client's credit balance.</param>
    public FulfillPaidOrderHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        ILogger<FulfillPaidOrderHandler> logger,
        IClock clock,
        IClientCredit clientCredit)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
        _clock = clock;
        _clientCredit = clientCredit;
    }

    /// <summary>Handles <see cref="FulfillPaidOrderCommand"/>.</summary>
    /// <param name="cmd">The fulfill command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success once fulfillment has run (a non-Pending order is a no-op, and still Success), or
    /// <c>ORDER_NOT_FOUND</c> when the id names no order, <c>ORDER_NOT_INVOICED</c> when the order
    /// carries no linked invoice; such an order is left Pending.
    /// Every other failure, a rejected domain or a provisioning fault, is reported per line,
    /// refunded or logged, and never turns the whole command into a refusal.
    /// </returns>
    public async Task<Result> HandleAsync(FulfillPaidOrderCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct);
        if (order is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_FOUND);
        }

        if (order.Status != OrderStatus.Pending)
        {
            _logger.LogInformation(
                "Order {OrderId} is {Status}; fulfillment already ran, skipping.", order.Id, order.Status);
            return Result.Success();
        }

        // Checked before acceptance: an order with no invoice was never paid, so it stays Pending.
        if (order.InvoiceId is not { } invoiceId)
        {
            return Result.FailureCode(HostpanelErrorCodes.ORDER_NOT_INVOICED);
        }

        // The lines are claimed in the same save that accepts the order, so a crash after it leaves them claimed and the
        // order Incomplete (retryable once the claim runs out) instead of Active over lines that never started.
        var now = _clock.UtcNow;
        order.Accept(now);
        order.BeginFulfilment(now);
        await _context.SaveChangesAsync(ct);

        var fulfilled = await _bus.InvokeAsync<Result<OrderFulfillmentDto>>(new FulfillOrderLinesCommand(order.Id), ct);
        if (fulfilled.IsFailure)
        {
            return Result.FailureCode(fulfilled.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        // A service line that failed is already logged loud by the line fulfilment; a rejected
        // domain line is given back the way it was paid.
        foreach (var failure in fulfilled.Value!.FailedLines)
        {
            if (failure is not { Kind: OrderLineKind.Domain, Domain: { } domainName })
            {
                continue;
            }

            if (failure.ErrorCode == HostpanelErrorCodes.HOSTPANEL_UNEXPECTED)
            {
                // The action threw, so what the registrar did is unknown: the domain may well have been registered.
                // Refunding now could give the client the money back and the domain too. The line stays failed for
                // staff, and a retry finds a domain the client already holds and counts it as done.
                _logger.LogWarning(
                    "Domain line {ItemId} of order {OrderId} ('{DomainName}') ended with an unknown outcome and is not refunded; " +
                    "retry it or refund it by hand.",
                    failure.ItemId, order.Id, domainName);
                continue;
            }

            await HandleDomainRegistrationFailedAsync(order.Id, invoiceId, failure, order.ClientId, domainName, ct);
        }

        return Result.Success();
    }

    /// <summary>Renders a failed <see cref="Result"/> as the one sentence a log line needs.</summary>
    /// <param name="result">The failed outcome to describe.</param>
    /// <returns>The result's message, its code, or a generic sentence, whichever is present first.</returns>
    private static string DescribeFailure(Result result) =>
        result.Error ?? result.ErrorCode ?? "The operation failed.";

    /// <summary>
    /// Gives back what the failed line cost, split the way it was paid, as Billing's
    /// <see cref="GetInvoiceLineSharesQuery"/> reports it: its credit share goes back on the client's
    /// credit balance, and its paid share with the invoice's tax goes back through
    /// <see cref="RefundInvoicePaymentCommand"/>, which calls whichever gateway took it and writes the
    /// refund and its ledger row. A line paid wholly with credit is refunded to credit only.
    /// <para>
    /// The line is claimed first (<see cref="BeginOrderLineRefundCommand"/>: Refunding), so a retry that runs in the
    /// meantime is refused and the line is never both delivered and refunded; a line a retry already holds is not
    /// refunded at all. The claim ends one of three ways: the whole cost is back (Refunded, and
    /// <see cref="DomainRegistrationFailedEvent"/> is published), nothing came back (the line returns to Failed so it can
    /// be retried), or only part did (PartiallyRefunded, for staff to finish by hand).
    /// </para>
    /// </summary>
    /// <param name="orderId">The order the failed line belongs to.</param>
    /// <param name="invoiceId">The invoice that was paid.</param>
    /// <param name="failure">The order line whose domain action failed; its first payment, with the invoice's tax, is what is refunded.</param>
    /// <param name="clientId">The client who placed the order.</param>
    /// <param name="domainName">The domain name that failed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleDomainRegistrationFailedAsync(
        int orderId, int invoiceId, OrderLineFailureDto failure, int clientId, string domainName, CancellationToken ct)
    {
        var reason = failure.Reason;

        // A failure the run could not record leaves the line in progress under the run's attempt; naming that attempt lets
        // the refund claim it all the same, so a refused domain is never left charged because a save was lost.
        var claimed = await _bus.InvokeLineChangeAsync(
            new BeginOrderLineRefundCommand(orderId, failure.ItemId, failure.UnrecordedAttempt, failure.ErrorCode), ct);
        if (claimed.IsFailure)
        {
            // A retry holds the line, or it is settled already: that run owns what happens to the line now.
            _logger.LogWarning(
                "Domain line {ItemId} of order {OrderId} ('{DomainName}') was not refunded: {Refusal}.",
                failure.ItemId, orderId, domainName, DescribeFailure(claimed));
            return;
        }

        _logger.LogError(
            "Domain registration failed for '{DomainName}' (client {ClientId}): {Reason}. Issuing refund.",
            domainName, clientId, reason);

        // What the refund did so far, so the claim can be settled truthfully however it ended.
        var mayHaveMoved = false;
        var returnedSomething = false;
        var complete = false;
        Exception? fault = null;
        try
        {
            var shares = await LoadLineSharesAsync(invoiceId, failure.FirstPaymentAmount, ct);

            var creditBack = shares.CreditShare <= 0;
            if (shares.CreditShare > 0)
            {
                // The credit share is already spent, so it is added and saved at once; a concurrent
                // balance change is retried once there, a second conflict lands in the catch below.
                mayHaveMoved = true;
                var credited = await _clientCredit.AddAndSaveAsync(new ClientCreditChange(clientId, shares.CreditShare, CreditMovementType.InvoiceReturned, invoiceId), ct);
                if (credited.IsFailure)
                {
                    _logger.LogCritical(
                        "CREDIT REFUND FAILED for domain '{DomainName}' (client {ClientId}, invoice {InvoiceId}): {Refusal}. " +
                        "Manual credit of {Amount} required. Original failure: {Reason}",
                        domainName, clientId, invoiceId, DescribeFailure(credited), shares.CreditShare, reason);
                }
                else
                {
                    creditBack = true;
                    returnedSomething = true;
                }
            }

            if (creditBack && shares.PaidShare > 0)
            {
                mayHaveMoved = true;
                var refund = await _bus.InvokeAsync<Result>(
                    new RefundInvoicePaymentCommand(
                        invoiceId,
                        shares.PaidShare,
                        RefundType.Gateway,
                        Notes: $"Automatic refund: domain registration failed for '{domainName}'. Reason: {reason}"),
                    ct);
                if (refund.IsFailure)
                {
                    // A refusal part way through still records what the gateway did return, so the
                    // manual refund is only what is left of this line's paid share.
                    var refundedNow = (await LoadLineSharesAsync(invoiceId, failure.FirstPaymentAmount, ct)).AmountRefunded
                        - shares.AmountRefunded;
                    returnedSomething |= refundedNow > 0m;
                    _logger.LogCritical(
                        "REFUND FAILED for domain '{DomainName}' (client {ClientId}, invoice {InvoiceId}): {Refusal}. " +
                        "Manual refund of the remaining {Remaining} required. Original failure: {Reason}",
                        domainName, clientId, invoiceId, DescribeFailure(refund), Math.Max(0m, shares.PaidShare - refundedNow), reason);
                }
                else
                {
                    returnedSomething = true;
                    complete = true;
                }
            }
            else if (creditBack)
            {
                complete = true;
            }
        }
        catch (Exception refundEx)
        {
            // The domain was never created so no cleanup is needed there, but the client was
            // charged and could not be refunded automatically.
            fault = refundEx;
            _logger.LogCritical(
                refundEx,
                "REFUND FAILED for domain '{DomainName}' (client {ClientId}, invoice {InvoiceId}). " +
                "Manual refund required. Original failure: {Reason}",
                domainName, clientId, invoiceId, reason);
        }

        // A fault after money may have moved leaves the outcome unknown, which is treated as part done: staff check it,
        // and no retry can deliver a line that may have been paid back.
        var partlyRefunded = returnedSomething || (fault is not null && mayHaveMoved);
        await SettleRefundAsync(orderId, failure.ItemId, complete && fault is null, partlyRefunded, domainName, clientId, invoiceId, reason);

        if (fault is OperationCanceledException && ct.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
        }
    }

    /// <summary>
    /// Ends the claim on a line whose refund ran: closes it when the whole cost is back, otherwise returns it to failed
    /// (nothing came back) or leaves it partly refunded (some did). Not cancellable, because the money has moved by now
    /// and the line must say so.
    /// </summary>
    /// <param name="orderId">The order the line belongs to.</param>
    /// <param name="itemId">The line that was being refunded.</param>
    /// <param name="refundedInFull">Whether the whole cost went back.</param>
    /// <param name="partlyRefunded">Whether some of the cost went back, or may have.</param>
    /// <param name="domainName">The domain name that failed.</param>
    /// <param name="clientId">The client who placed the order.</param>
    /// <param name="invoiceId">The invoice that was paid.</param>
    /// <param name="reason">Why the line failed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SettleRefundAsync(
        int orderId, int itemId, bool refundedInFull, bool partlyRefunded, string domainName, int clientId, int invoiceId, string reason)
    {
        if (!refundedInFull)
        {
            var released = await _bus.InvokeLineChangeAsync(
                new ReleaseOrderLineRefundCommand(
                    orderId, itemId, partlyRefunded ? OrderLineRefundOutcome.PartlyRefunded : OrderLineRefundOutcome.NothingRefunded),
                CancellationToken.None);
            if (released.IsFailure)
            {
                _logger.LogCritical(
                    "Order {OrderId} line {ItemId} could not be settled after its refund ended: {Reason}. The line stays Refunding, " +
                    "which no retry can take, until staff settle it by hand.",
                    orderId, itemId, DescribeFailure(released));
            }

            return;
        }

        var closed = await _bus.InvokeLineChangeAsync(new RecordOrderLineRefundedCommand(orderId, itemId), CancellationToken.None);
        if (closed.IsFailure)
        {
            _logger.LogCritical(
                "Order {OrderId} line {ItemId} was refunded but could not be closed: {Reason}. The line stays Refunding, " +
                "which no retry can take, until staff settle it by hand.",
                orderId, itemId, DescribeFailure(closed));
            return;
        }

        try
        {
            await _bus.PublishAsync(new DomainRegistrationFailedEvent(clientId, domainName, invoiceId, reason) { OccurredAt = _clock.UtcNow });
        }
        catch (Exception publishEx)
        {
            // The money is back and the line is closed; only the notification of it is lost.
            _logger.LogError(
                publishEx, "The registration-failed event of domain '{DomainName}' (client {ClientId}) could not be published.", domainName, clientId);
        }
    }

    /// <summary>Reads how the line was paid and what the invoice has refunded so far, from Billing.</summary>
    /// <param name="invoiceId">The invoice that was paid.</param>
    /// <param name="lineAmount">The line's first payment, before tax.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The line's shares as the invoice stands now.</returns>
    /// <exception cref="InvalidOperationException">The paid invoice no longer exists; caught by the refund path as a manual-refund case.</exception>
    private async Task<InvoiceLineSharesDto> LoadLineSharesAsync(int invoiceId, decimal lineAmount, CancellationToken ct)
    {
        var shares = await _bus.InvokeAsync<Result<InvoiceLineSharesDto>>(new GetInvoiceLineSharesQuery(invoiceId, lineAmount), ct);
        if (shares.IsFailure)
        {
            throw new InvalidOperationException($"Invoice {invoiceId} could not be read: {DescribeFailure(shares)}.");
        }

        return shares.Value!;
    }

    /// <summary>Renders a failed <see cref="Result{T}"/> as the one sentence a log line needs.</summary>
    /// <typeparam name="T">The value type of a successful result.</typeparam>
    /// <param name="result">The failed outcome to describe.</param>
    /// <returns>The result's message, its code, or a generic sentence, whichever is present first.</returns>
    private static string DescribeFailure<T>(Result<T> result) =>
        result.Error ?? result.ErrorCode ?? "The operation failed.";
}
