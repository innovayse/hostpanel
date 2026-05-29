namespace Innovayse.Domain.Orders;

using Innovayse.Domain.Common;
using Innovayse.Domain.Orders.Events;

/// <summary>
/// Represents a purchase order placed by a client at checkout.
/// An order aggregates one or more <see cref="OrderItem"/> entries and links
/// to an <see cref="InvoiceId"/> for payment tracking.
/// The admin reviews pending orders and accepts, cancels, or marks them as fraud.
/// </summary>
public sealed class Order : AggregateRoot
{
    /// <summary>Gets the human-readable order number (e.g. "ORD-0001").</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Gets the payment gateway module name selected at checkout.</summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the linked invoice, or null if not yet created.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the client's IP address at checkout time.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the admin notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the UTC timestamp when the order was placed.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Internal mutable list of order items.</summary>
    private readonly List<OrderItem> _items = [];

    /// <summary>Gets the read-only collection of order items.</summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Order() : base(0) { }

    /// <summary>
    /// Creates a new pending order and raises <see cref="OrderCreatedEvent"/>.
    /// </summary>
    /// <param name="orderNumber">Human-readable order number (e.g. "ORD-0001").</param>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="paymentMethod">Payment gateway module name.</param>
    /// <param name="ipAddress">Client's IP address at checkout.</param>
    /// <returns>A new pending <see cref="Order"/>.</returns>
    public static Order Create(string orderNumber, int clientId, string paymentMethod, string? ipAddress)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            ClientId = clientId,
            PaymentMethod = paymentMethod,
            IpAddress = ipAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        order.AddDomainEvent(new OrderCreatedEvent(0, clientId));
        return order;
    }

    /// <summary>
    /// Adds a line item to this order with snapshotted product data.
    /// </summary>
    /// <param name="productId">FK to the product.</param>
    /// <param name="productName">Product name at order time.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="domain">Optional domain name for hosting products.</param>
    /// <param name="hostname">Optional hostname for VPS/server products.</param>
    public void AddItem(
        int productId,
        string productName,
        string billingCycle,
        decimal firstPaymentAmount,
        decimal recurringAmount,
        string? domain,
        string? hostname)
    {
        _items.Add(OrderItem.Create(productId, productName, billingCycle, firstPaymentAmount, recurringAmount, domain, hostname));
    }

    /// <summary>
    /// Accepts the order, transitioning from Pending to Active.
    /// Raises <see cref="OrderAcceptedEvent"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void Accept()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot accept an order with status {Status}. Only Pending orders can be accepted.");
        }

        Status = OrderStatus.Active;

        foreach (var item in _items)
        {
            item.MarkActive();
        }

        AddDomainEvent(new OrderAcceptedEvent(Id, ClientId));
    }

    /// <summary>
    /// Cancels the order, transitioning from Pending to Cancelled.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void Cancel()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel an order with status {Status}. Only Pending orders can be cancelled.");
        }

        Status = OrderStatus.Cancelled;

        foreach (var item in _items)
        {
            item.MarkCancelled();
        }
    }

    /// <summary>
    /// Marks the order as fraudulent.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void MarkFraud()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot mark an order as fraud with status {Status}. Only Pending orders can be flagged.");
        }

        Status = OrderStatus.Fraud;

        foreach (var item in _items)
        {
            item.MarkCancelled();
        }
    }

    /// <summary>
    /// Links this order to an invoice for payment tracking.
    /// </summary>
    /// <param name="invoiceId">FK to the invoice.</param>
    public void LinkInvoice(int invoiceId)
    {
        InvoiceId = invoiceId;
    }

    /// <summary>
    /// Sets or clears the admin notes.
    /// </summary>
    /// <param name="notes">Admin notes text, or null to clear.</param>
    public void SetNotes(string? notes)
    {
        Notes = notes;
    }
}
