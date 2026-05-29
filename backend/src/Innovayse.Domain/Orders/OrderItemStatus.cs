namespace Innovayse.Domain.Orders;

/// <summary>Lifecycle status of an individual order item.</summary>
public enum OrderItemStatus
{
    /// <summary>Item pending, not yet fulfilled.</summary>
    Pending,

    /// <summary>Item fulfilled, service created.</summary>
    Active,

    /// <summary>Item cancelled.</summary>
    Cancelled,
}
