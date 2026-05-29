namespace Innovayse.Domain.Orders;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a single line item within an <see cref="Order"/>.
/// Snapshots the product name and price at order time so price changes
/// after ordering do not affect existing orders.
/// </summary>
public sealed class OrderItem : Entity
{
    /// <summary>Gets the FK to the parent order.</summary>
    public int OrderId { get; private set; }

    /// <summary>Gets the FK to the ordered product.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the product name snapshot at order time.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Gets the selected billing cycle ("monthly" or "annual").</summary>
    public string BillingCycle { get; private set; } = string.Empty;

    /// <summary>Gets the optional domain name for hosting products.</summary>
    public string? Domain { get; private set; }

    /// <summary>Gets the optional hostname for VPS/server products.</summary>
    public string? Hostname { get; private set; }

    /// <summary>Gets the first payment amount snapshot at order time.</summary>
    public decimal FirstPaymentAmount { get; private set; }

    /// <summary>Gets the recurring charge amount snapshot at order time.</summary>
    public decimal RecurringAmount { get; private set; }

    /// <summary>Gets the current lifecycle status of this item.</summary>
    public OrderItemStatus Status { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private OrderItem() : base(0) { }

    /// <summary>
    /// Creates a new pending order item with snapshotted product data.
    /// </summary>
    /// <param name="productId">FK to the product.</param>
    /// <param name="productName">Product name at order time.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="domain">Optional domain name.</param>
    /// <param name="hostname">Optional hostname.</param>
    /// <returns>A new pending <see cref="OrderItem"/>.</returns>
    internal static OrderItem Create(
        int productId,
        string productName,
        string billingCycle,
        decimal firstPaymentAmount,
        decimal recurringAmount,
        string? domain,
        string? hostname)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            BillingCycle = billingCycle,
            FirstPaymentAmount = firstPaymentAmount,
            RecurringAmount = recurringAmount,
            Domain = domain,
            Hostname = hostname,
            Status = OrderItemStatus.Pending,
        };
    }

    /// <summary>Marks this item as fulfilled.</summary>
    internal void MarkActive() => Status = OrderItemStatus.Active;

    /// <summary>Marks this item as cancelled.</summary>
    internal void MarkCancelled() => Status = OrderItemStatus.Cancelled;
}
