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

    /// <summary>Internal mutable list of transactions.</summary>
    private readonly List<InvoiceTransaction> _transactions = [];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>Gets the invoice issue date (UTC).</summary>
    public DateTimeOffset InvoiceDate { get; private set; }

    /// <summary>Gets the payment due date (UTC).</summary>
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when payment was received; null if unpaid.</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Gets the sum of all line item amounts before tax and credit.</summary>
    public decimal SubTotal { get; private set; }

    /// <summary>Gets the tax rate percentage applied to the invoice.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Gets the computed tax amount (SubTotal × TaxRate / 100).</summary>
    public decimal Tax { get; private set; }

    /// <summary>Gets the total credit applied to the invoice.</summary>
    public decimal Credit { get; private set; }

    /// <summary>Gets the final total (SubTotal + Tax − Credit).</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the payment gateway transaction reference; null until paid.</summary>
    public string? GatewayTransactionId { get; private set; }

    /// <summary>Gets the external system ID (e.g. source system invoice ID) used for migration deduplication.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Gets optional notes for this invoice.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the preferred payment method; null when not specified.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the read-only view of invoice line items.</summary>
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    /// <summary>Gets the read-only view of invoice transactions.</summary>
    public IReadOnlyList<InvoiceTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Invoice() : base(0) { }

    /// <summary>
    /// Creates a new draft invoice (not yet sent to client).
    /// </summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Draft"/> status.</returns>
    public static Invoice CreateDraft(int clientId, DateTimeOffset dueDate)
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            ClientId = clientId,
            Status = InvoiceStatus.Draft,
            InvoiceDate = now,
            DueDate = dueDate,
            CreatedAt = now,
        };
        invoice.AddDomainEvent(new InvoiceCreatedEvent(0, clientId));
        return invoice;
    }

    /// <summary>
    /// Creates a new invoice, optionally as a draft.
    /// </summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="isDraft">When true, creates a Draft; otherwise creates an Unpaid invoice.</param>
    /// <returns>A new <see cref="Invoice"/>.</returns>
    public static Invoice Create(int clientId, DateTimeOffset dueDate, bool isDraft) =>
        isDraft ? CreateDraft(clientId, dueDate) : Create(clientId, dueDate);

    /// <summary>
    /// Creates a new unpaid invoice.
    /// </summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Unpaid"/> status.</returns>
    public static Invoice Create(int clientId, DateTimeOffset dueDate)
    {
        var now = DateTimeOffset.UtcNow;
        var invoice = new Invoice
        {
            ClientId = clientId,
            Status = InvoiceStatus.Unpaid,
            InvoiceDate = now,
            DueDate = dueDate,
            CreatedAt = now,
        };
        invoice.AddDomainEvent(new InvoiceCreatedEvent(0, clientId));
        return invoice;
    }

    /// <summary>Sets the external system ID for migration deduplication.</summary>
    public void SetExternalId(string externalId) => ExternalId = externalId;

    /// <summary>
    /// Publishes a draft invoice, transitioning it to Unpaid status.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not in Draft status.</exception>
    public void Publish()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft invoices can be published; current status is {Status}.");
        }

        Status = InvoiceStatus.Unpaid;
    }

    /// <summary>
    /// Adds a line item and recalculates totals.
    /// </summary>
    public void AddItem(string description, decimal unitPrice, int quantity)
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid))
        {
            throw new InvalidOperationException($"Cannot add items to an invoice with status {Status}.");
        }

        var item = InvoiceItem.Create(description, unitPrice, quantity);
        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a line item by ID and recalculates totals.
    /// </summary>
    public void RemoveItem(int itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Invoice item {itemId} not found.");
        _items.Remove(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Updates an existing line item and recalculates totals.
    /// </summary>
    public void UpdateItem(int itemId, string description, decimal unitPrice, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Invoice item {itemId} not found.");
        item.Update(description, unitPrice, quantity);
        RecalculateTotals();
    }

    /// <summary>
    /// Records a payment transaction against this invoice.
    /// </summary>
    public void AddPayment(DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees, string? notes)
    {
        var tx = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Payment, fees, notes);
        _transactions.Add(tx);
        MarkPaid(transactionId);
    }

    /// <summary>
    /// Updates invoice options (issue date, due date, payment method, tax rate).
    /// </summary>
    public void UpdateOptions(DateTimeOffset invoiceDate, DateTimeOffset dueDate, string? paymentMethod, decimal taxRate)
    {
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        PaymentMethod = paymentMethod;
        TaxRate = taxRate;
        RecalculateTotals();
    }

    /// <summary>
    /// Applies a credit amount to the invoice, reducing the total owed.
    /// </summary>
    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive.", nameof(amount));
        Credit += amount;
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a credit amount from the invoice.
    /// </summary>
    public void RemoveCredit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive.", nameof(amount));
        Credit = Math.Max(0, Credit - amount);
        RecalculateTotals();
    }

    /// <summary>
    /// Updates or clears the invoice notes.
    /// </summary>
    public void UpdateNotes(string? notes) => Notes = notes;

    /// <summary>
    /// Records a successful payment and raises <see cref="PaymentReceivedEvent"/>.
    /// </summary>
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
    public void MarkOverdue()
    {
        if (Status == InvoiceStatus.Overdue) return;

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
    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot cancel a paid invoice.");
        }

        Status = InvoiceStatus.Cancelled;
    }

    /// <summary>
    /// Reverses a paid invoice back to Unpaid status.
    /// </summary>
    public void MarkUnpaid()
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException($"Only Paid invoices can be marked Unpaid; current status is {Status}.");
        }

        Status = InvoiceStatus.Unpaid;
        PaidAt = null;
        GatewayTransactionId = null;
    }

    /// <summary>
    /// Records a refund transaction against this invoice.
    /// </summary>
    public void AddRefund(DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees, string? notes)
    {
        var tx = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Refund, fees, notes);
        _transactions.Add(tx);
        Refund();
    }

    /// <summary>
    /// Creates a new Draft copy of this invoice (with all its line items).
    /// </summary>
    public Invoice Duplicate()
    {
        var copy = CreateDraft(ClientId, DueDate);
        foreach (var item in _items)
        {
            copy.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }
        return copy;
    }

    /// <summary>
    /// Refunds a paid invoice and marks it as Refunded.
    /// </summary>
    public void Refund()
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException($"Only Paid invoices can be refunded; current status is {Status}.");
        }

        Status = InvoiceStatus.Refunded;
        PaidAt = null;
        GatewayTransactionId = null;
    }

    private void RecalculateTotals()
    {
        SubTotal = _items.Sum(i => i.Amount);
        Tax = Math.Round(SubTotal * TaxRate / 100, 4);
        Total = SubTotal + Tax - Credit;
    }
}
