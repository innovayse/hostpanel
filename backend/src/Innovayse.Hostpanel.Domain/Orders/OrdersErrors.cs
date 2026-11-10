namespace Innovayse.Hostpanel.Domain.Orders;

/// <summary>
/// Refusal codes the <see cref="Order"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string value from
/// this constant so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for it in every language.
/// </remarks>
public static class OrdersErrors
{
    /// <summary>
    /// The order is not Pending (400): <see cref="Order.Accept"/>, <see cref="Order.Cancel"/> and
    /// <see cref="Order.MarkFraud"/> all require it.
    /// </summary>
    public const string OrderNotPending = "ORDER_NOT_PENDING";

    /// <summary>
    /// The order was not accepted (400): only an Active or Incomplete order has lines to fulfil,
    /// <see cref="Order.BeginFulfilment"/>.
    /// </summary>
    public const string OrderNotAccepted = "ORDER_NOT_ACCEPTED";

    /// <summary>
    /// The line may not be retried (409): the order was not accepted, the line did not fail, was refunded, or
    /// another retry is running it: <see cref="Order.StartLineRetry"/>.
    /// </summary>
    public const string OrderLineNotRetryable = "ORDER_LINE_NOT_RETRYABLE";

    /// <summary>
    /// The order holds no line with that id (404): <see cref="Order.RecordLineFulfilled"/>,
    /// <see cref="Order.RecordLineFailed"/> and <see cref="Order.RecordLineRefunded"/>.
    /// </summary>
    public const string OrderItemNotFound = "ORDER_ITEM_NOT_FOUND";

    /// <summary>
    /// The line changed while an attempt ran (409): another writer saved it first, or it is being or was refunded,
    /// so the attempt's outcome cannot be recorded: <see cref="Order.RecordLineFulfilled"/> and
    /// <see cref="Order.RecordLineFailed"/>.
    /// </summary>
    public const string OrderLineChanged = "ORDER_LINE_CHANGED";

    /// <summary>
    /// The line is not in the state a refund needs (409): only a failed line can start giving its cost back, and only
    /// a line being refunded can be settled: <see cref="Order.BeginLineRefund"/>, <see cref="Order.RecordLineRefunded"/>
    /// and <see cref="Order.RecordLineRefundReleased"/>.
    /// </summary>
    public const string OrderLineNotRefundable = "ORDER_LINE_NOT_REFUNDABLE";

    /// <summary>
    /// The line's refund may still be running (409): it began less than the retry lease ago, so closing it by hand could
    /// have the cost given back twice: <see cref="Order.CloseLineRefund"/>.
    /// </summary>
    public const string OrderLineRefundInProgress = "ORDER_LINE_REFUND_IN_PROGRESS";
}
