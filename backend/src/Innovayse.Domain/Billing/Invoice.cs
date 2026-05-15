namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Common;

/// <summary>
/// An invoice issued to a client for services or products.
/// Owns a collection of <see cref="InvoiceItem"/> line items.
/// Payment is recorded inline via <see cref="GatewayTransactionId"/> and <see cref="PaidAt"/>.
/// Stored in the <c>invoices</c> table.
/// </summary>
public sealed class Invoice : AggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<InvoiceItem> _items = [];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>Gets the payment due date (UTC).</summary>
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when payment was received; null if unpaid.</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Gets the running total of all line items.</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the payment gateway transaction reference; null until paid.</summary>
    public string? GatewayTransactionId { get; private set; }

    /// <summary>Gets the read-only view of invoice line items.</summary>
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Invoice() : base(0) { }

    /// <summary>
    /// Creates a new unpaid invoice and raises <see cref="InvoiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Unpaid"/> status.</returns>
    public static Invoice Create(int clientId, DateTimeOffset dueDate)
    {
        var invoice = new Invoice
        {
            ClientId = clientId,
            Status = InvoiceStatus.Unpaid,
            DueDate = dueDate,
            CreatedAt = DateTimeOffset.UtcNow,
            Total = 0m,
        };
        invoice.AddDomainEvent(new InvoiceCreatedEvent(0, clientId));
        return invoice;
    }

    /// <summary>
    /// Adds a line item and recalculates <see cref="Total"/>.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is already paid or cancelled.</exception>
    public void AddItem(string description, decimal unitPrice, int quantity)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot add items to an invoice with status {Status}.");
        }

        var item = InvoiceItem.Create(description, unitPrice, quantity);
        _items.Add(item);
        Total += item.Amount;
    }

    /// <summary>
    /// Records a successful payment and raises <see cref="PaymentReceivedEvent"/>.
    /// </summary>
    /// <param name="gatewayTransactionId">The transaction reference from the payment gateway.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in a payable state (Unpaid or Overdue).</exception>
    public void MarkPaid(string gatewayTransactionId)
    {
        if (Status is not (InvoiceStatus.Unpaid or InvoiceStatus.Overdue))
        {
            throw new InvalidOperationException($"Invoice cannot be paid in status {Status}.");
        }

        Status = InvoiceStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
        GatewayTransactionId = gatewayTransactionId;
        AddDomainEvent(new PaymentReceivedEvent(Id, ClientId, Total, gatewayTransactionId));
    }

    /// <summary>
    /// Transitions the invoice from Unpaid to Overdue and raises <see cref="InvoiceOverdueEvent"/>.
    /// If already Overdue, this is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not Unpaid or already Overdue.</exception>
    public void MarkOverdue()
    {
        if (Status == InvoiceStatus.Overdue)
        {
            return; // already overdue — idempotent, no duplicate event
        }

        if (Status != InvoiceStatus.Unpaid)
        {
            throw new InvalidOperationException($"Only Unpaid invoices can be marked overdue; current status is {Status}.");
        }

        Status = InvoiceStatus.Overdue;
        AddDomainEvent(new InvoiceOverdueEvent(Id, ClientId));
    }

    /// <summary>
    /// Cancels the invoice so it will not be collected.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is already paid.</exception>
    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot cancel a paid invoice.");
        }

        Status = InvoiceStatus.Cancelled;
    }
}
