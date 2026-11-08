namespace Innovayse.Hostpanel.Domain.Orders.Enums;

/// <summary>
/// Where fulfilling one order line stands: what the order's acceptance or a later retry made of it.
/// Kept apart from <see cref="OrderItemStatus"/>, which says whether the line was accepted, cancelled
/// or never reviewed.
/// </summary>
public enum OrderItemFulfilmentState
{
    /// <summary>No attempt has been recorded yet (a line of a pending order, or one accepted before attempts were recorded).</summary>
    Pending = 0,

    /// <summary>A retry has claimed the line and is running; no second retry may start until it records its outcome.</summary>
    InProgress = 1,

    /// <summary>The line was fulfilled: the service was created or the domain action done.</summary>
    Fulfilled = 2,

    /// <summary>The last attempt failed with a coded reason; staff may retry the line.</summary>
    Failed = 3,

    /// <summary>The line failed and what it cost was given back, so it is closed and must not be fulfilled any more.</summary>
    Refunded = 4,
}
