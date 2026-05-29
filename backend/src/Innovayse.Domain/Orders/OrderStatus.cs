namespace Innovayse.Domain.Orders;

/// <summary>Lifecycle status of an order.</summary>
public enum OrderStatus
{
    /// <summary>Order placed, awaiting admin review.</summary>
    Pending,

    /// <summary>Order accepted by admin, services created.</summary>
    Active,

    /// <summary>Order cancelled by admin or client.</summary>
    Cancelled,

    /// <summary>Order flagged as fraudulent.</summary>
    Fraud,
}
