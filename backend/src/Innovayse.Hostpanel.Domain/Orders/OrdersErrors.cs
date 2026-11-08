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
    /// The line may not be retried (409): the order was not accepted, the line did not fail, was refunded, or
    /// another retry is running it: <see cref="Order.StartLineRetry"/>.
    /// </summary>
    public const string OrderLineNotRetryable = "ORDER_LINE_NOT_RETRYABLE";

    /// <summary>
    /// The order holds no line with that id (404): <see cref="Order.RecordLineFulfilled"/>,
    /// <see cref="Order.RecordLineFailed"/> and <see cref="Order.RecordLineRefunded"/>.
    /// </summary>
    public const string OrderItemNotFound = "ORDER_ITEM_NOT_FOUND";
}
