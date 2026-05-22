namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Common;

/// <summary>
/// An invoice issued to a client for services or products.
/// Owns a collection of <see cref="InvoiceItem"/> line items and <see cref="InvoiceTransaction"/> records.
/// Stored in the <c>invoices</c> table.
/// </summary>
public sealed class Invoice : AggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<InvoiceItem> _items = [];

    /// <summary>Internal mutable list of financial transactions.</summary>
    private readonly List<InvoiceTransaction> _transactions = [];

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

    /// <summary>Gets the running total of all line items after tax and credit adjustments.</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the payment gateway transaction reference; null until paid.</summary>
    public string? GatewayTransactionId { get; private set; }

    /// <summary>Gets optional notes attached to the invoice.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the invoice issue date (UTC).</summary>
    public DateTimeOffset InvoiceDate { get; private set; }

    /// <summary>Gets the preferred payment method for this invoice; null when not specified.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the tax rate percentage applied to the subtotal.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Gets the computed tax amount (SubTotal × TaxRate / 100).</summary>
    public decimal Tax { get; private set; }

    /// <summary>Gets the sum of all line item amounts before tax and credit.</summary>
    public decimal SubTotal { get; private set; }

    /// <summary>Gets the total credit applied to this invoice.</summary>
    public decimal Credit { get; private set; }

    /// <summary>Gets the read-only view of invoice line items.</summary>
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    /// <summary>Gets the read-only view of financial transactions.</summary>
    public IReadOnlyList<InvoiceTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Invoice() : base(0) { }

    /// <summary>
    /// Creates a new invoice and raises <see cref="InvoiceCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="isDraft">When <see langword="true"/>, creates a Draft invoice; otherwise creates Unpaid.</param>
    /// <returns>A new <see cref="Invoice"/>.</returns>
    public static Invoice Create(int clientId, DateTimeOffset dueDate, bool isDraft = false)
    {
        var invoice = new Invoice
        {
            ClientId = clientId,
            Status = isDraft ? InvoiceStatus.Draft : InvoiceStatus.Unpaid,
            DueDate = dueDate,
            CreatedAt = DateTimeOffset.UtcNow,
            InvoiceDate = DateTimeOffset.UtcNow,
            Total = 0m,
            SubTotal = 0m,
            Tax = 0m,
            TaxRate = 0m,
            Credit = 0m,
        };
        invoice.AddDomainEvent(new InvoiceCreatedEvent(0, clientId));
        return invoice;
    }

    /// <summary>
    /// Adds a line item and recalculates totals.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice status does not allow modifications.</exception>
    public void AddItem(string description, decimal unitPrice, int quantity)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot add items to an invoice with status {Status}.");
        }

        var item = InvoiceItem.Create(description, unitPrice, quantity);
        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Updates an existing line item and recalculates totals.
    /// </summary>
    /// <param name="itemId">The ID of the item to update.</param>
    /// <param name="description">New human-readable charge description.</param>
    /// <param name="unitPrice">New price per unit (≥ 0).</param>
    /// <param name="quantity">New number of units (≥ 1).</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice status does not allow modifications.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found on this invoice.</exception>
    public void UpdateItem(int itemId, string description, decimal unitPrice, int quantity)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot update items on an invoice with status {Status}.");
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found on invoice {Id}.");

        item.Update(description, unitPrice, quantity);
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a line item by ID and recalculates totals.
    /// </summary>
    /// <param name="itemId">The ID of the item to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice status does not allow modifications.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found on this invoice.</exception>
    public void RemoveItem(int itemId)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot remove items from an invoice with status {Status}.");
        }

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found on invoice {Id}.");

        _items.Remove(item);
        RecalculateTotals();
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
            return;
        }

        if (Status != InvoiceStatus.Unpaid)
        {
            throw new InvalidOperationException($"Only Unpaid invoices can be marked overdue; current status is {Status}.");
        }

        Status = InvoiceStatus.Overdue;
        AddDomainEvent(new InvoiceOverdueEvent(Id, ClientId));
    }

    /// <summary>
    /// Reverses a paid invoice back to Unpaid and clears payment information.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Paid status.</exception>
    public void MarkUnpaid()
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException($"Only Paid invoices can be marked unpaid; current status is {Status}.");
        }

        Status = InvoiceStatus.Unpaid;
        PaidAt = null;
    }

    /// <summary>
    /// Cancels the invoice so it will not be collected.
    /// Allows cancelling Draft, Unpaid, and Overdue invoices.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is Paid or Refunded.</exception>
    public void Cancel()
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot cancel an invoice with status {Status}.");
        }

        Status = InvoiceStatus.Cancelled;
    }

    /// <summary>
    /// Publishes a draft invoice, transitioning it to Unpaid and raising <see cref="InvoicePublishedEvent"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Draft status.</exception>
    public void Publish()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft invoices can be published; current status is {Status}.");
        }

        Status = InvoiceStatus.Unpaid;
        AddDomainEvent(new InvoicePublishedEvent(Id, ClientId));
    }

    /// <summary>
    /// Updates invoice options (dates, payment method, tax rate) and recalculates totals.
    /// </summary>
    /// <param name="invoiceDate">The new invoice issue date.</param>
    /// <param name="dueDate">The new payment due date.</param>
    /// <param name="paymentMethod">The preferred payment method; null to clear.</param>
    /// <param name="taxRate">The tax rate percentage (0–100).</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is Paid or Refunded.</exception>
    public void UpdateOptions(DateTimeOffset invoiceDate, DateTimeOffset dueDate, string? paymentMethod, decimal taxRate)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Refunded)
        {
            throw new InvalidOperationException($"Cannot update options on an invoice with status {Status}.");
        }

        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        PaymentMethod = paymentMethod;
        TaxRate = taxRate;
        RecalculateTotals();
    }

    /// <summary>
    /// Updates the invoice notes. No status guard — notes can be set on any invoice.
    /// </summary>
    /// <param name="notes">The new notes text; null to clear.</param>
    public void UpdateNotes(string? notes)
    {
        Notes = notes;
    }

    /// <summary>
    /// Applies a credit amount to the invoice, reducing the total owed.
    /// A Credit transaction is recorded for audit purposes.
    /// </summary>
    /// <param name="amount">The credit amount to apply (must be greater than 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not greater than 0.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the credit would make the total negative.</exception>
    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be greater than 0.");
        }

        var maxCredit = SubTotal + Tax - Credit;
        if (amount > maxCredit)
        {
            throw new InvalidOperationException(
                $"Credit amount {amount} would exceed the remaining balance of {maxCredit}.");
        }

        Credit += amount;
        RecalculateTotals();

        var transaction = InvoiceTransaction.Create(
            Id, DateTimeOffset.UtcNow, "Credit", $"credit-{Id}-{DateTimeOffset.UtcNow.Ticks}",
            amount, InvoiceTransactionType.Credit);
        _transactions.Add(transaction);
    }

    /// <summary>
    /// Removes a specific credit amount from the invoice and recalculates totals.
    /// </summary>
    /// <param name="amount">The credit amount to remove (must be greater than 0 and not exceed applied credit).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not greater than 0.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="amount"/> exceeds the applied credit.</exception>
    public void RemoveCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than 0.");
        }

        if (amount > Credit)
        {
            throw new InvalidOperationException(
                $"Cannot remove {amount} — only {Credit} credit is applied.");
        }

        Credit -= amount;
        RecalculateTotals();
    }

    /// <summary>
    /// Records a payment transaction against this invoice.
    /// If total payments meet or exceed the invoice total, the invoice is automatically marked as paid.
    /// </summary>
    /// <param name="date">UTC timestamp of the payment.</param>
    /// <param name="gateway">Payment gateway name.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="amount">Payment amount (must be greater than 0).</param>
    /// <param name="fees">Transaction fees charged by the gateway (≥ 0).</param>
    /// <param name="notes">Optional payment notes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not Unpaid or Overdue.</exception>
    public void AddPayment(DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees = 0m, string? notes = null)
    {
        if (Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot add payment to an invoice with status {Status}.");
        }

        var transaction = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Payment, fees, notes);
        _transactions.Add(transaction);

        var totalPayments = _transactions
            .Where(t => t.Type == InvoiceTransactionType.Payment)
            .Sum(t => t.Amount);

        if (totalPayments >= Total && Status is InvoiceStatus.Unpaid or InvoiceStatus.Overdue or InvoiceStatus.Refunded)
        {
            MarkPaid(transactionId);
        }
    }

    /// <summary>
    /// Records a refund transaction and transitions the invoice to Refunded status.
    /// Raises <see cref="InvoiceRefundedEvent"/>.
    /// </summary>
    /// <param name="date">UTC timestamp of the refund.</param>
    /// <param name="gateway">Payment gateway name.</param>
    /// <param name="transactionId">External transaction reference.</param>
    /// <param name="amount">Refund amount (must be greater than 0).</param>
    /// <param name="fees">Transaction fees for the refund (≥ 0).</param>
    /// <param name="notes">Optional refund notes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Paid status.</exception>
    public void AddRefund(DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees = 0m, string? notes = null)
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException($"Can only refund Paid invoices; current status is {Status}.");
        }

        var transaction = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Refund, fees, notes);
        _transactions.Add(transaction);
        Status = InvoiceStatus.Refunded;
        AddDomainEvent(new InvoiceRefundedEvent(Id, ClientId, amount));
    }

    /// <summary>
    /// Creates a duplicate of this invoice as a new Draft, copying all line items.
    /// The new invoice has a fresh creation/invoice date and the same due date.
    /// </summary>
    /// <returns>A new Draft <see cref="Invoice"/> with copies of all line items.</returns>
    public Invoice Duplicate()
    {
        var copy = Create(ClientId, DueDate, isDraft: true);
        copy.TaxRate = TaxRate;
        copy.PaymentMethod = PaymentMethod;
        copy.Notes = Notes;

        foreach (var item in _items)
        {
            copy.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }

        return copy;
    }

    /// <summary>Recalculates <see cref="SubTotal"/>, <see cref="Tax"/>, and <see cref="Total"/> from current items, tax rate, and credit.</summary>
    private void RecalculateTotals()
    {
        SubTotal = _items.Sum(i => i.Amount);
        Tax = SubTotal * TaxRate / 100m;
        Total = SubTotal + Tax - Credit;
    }
}
