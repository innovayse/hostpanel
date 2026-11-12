namespace Innovayse.Hostpanel.Domain.Orders.Enums;

/// <summary>Lifecycle status of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order placed, awaiting admin review.</summary>
    Pending = 0,

    /// <summary>Order accepted by admin, services created.</summary>
    Active = 1,

    /// <summary>Order cancelled by admin or client.</summary>
    Cancelled = 2,

    /// <summary>Order flagged as fraudulent.</summary>
    Fraud = 3,

    /// <summary>
    /// Order accepted, but at least one line failed to be fulfilled (or a retry of it is running), so the
    /// order is not complete yet. It returns to <see cref="Active"/> when no such line is left.
    /// </summary>
    Incomplete = 4,

    /// <summary>
    /// Order held back for staff because its fraud score reached the review threshold. It is not accepted or fulfilled, and a
    /// payment for it fulfils nothing, until staff approve it (back to <see cref="Pending"/>) or reject it (<see cref="Fraud"/>).
    /// </summary>
    UnderReview = 5,
}
