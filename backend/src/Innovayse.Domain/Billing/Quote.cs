namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
<<<<<<< HEAD
/// A quotation (estimate) issued to a client for products or services.
/// Owns a collection of <see cref="QuoteItem"/> line items.
/// Can be accepted by the client and converted to an invoice.
=======
/// A quote (proposal) issued to a client for prospective services or products.
/// Owns a collection of <see cref="QuoteItem"/> line items.
>>>>>>> origin/main
/// Stored in the <c>quotes</c> table.
/// </summary>
public sealed class Quote : AggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<QuoteItem> _items = [];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

<<<<<<< HEAD
    /// <summary>Gets the quote subject/title.</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>Gets the current lifecycle status.</summary>
    public QuoteStatus Status { get; private set; }

    /// <summary>Gets the quote expiry date (UTC).</summary>
    public DateTimeOffset ExpiryDate { get; private set; }

    /// <summary>Gets optional notes or terms for the quote.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the running total of all line items.</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the UTC timestamp when the quote was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
=======
    /// <summary>Gets the quote subject / title.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the current lifecycle stage.</summary>
    public QuoteStage Stage { get; private set; }

    /// <summary>Gets the UTC timestamp when the quote was created.</summary>
    public DateTimeOffset DateCreated { get; private set; }

    /// <summary>Gets the date until which this quote is valid; null means no expiry.</summary>
    public DateTimeOffset? ValidUntil { get; private set; }

    /// <summary>Gets the sum of all line item amounts before any adjustments.</summary>
    public decimal SubTotal { get; private set; }

    /// <summary>Gets the running total of the quote (currently equal to SubTotal).</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the proposal text displayed at the top of the quote.</summary>
    public string? ProposalText { get; private set; }

    /// <summary>Gets customer-facing notes displayed in the quote footer.</summary>
    public string? CustomerNotes { get; private set; }

    /// <summary>Gets private admin-only notes not visible to the client.</summary>
    public string? AdminNotes { get; private set; }
>>>>>>> origin/main

    /// <summary>Gets the read-only view of quote line items.</summary>
    public IReadOnlyList<QuoteItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Quote() : base(0) { }

    /// <summary>
<<<<<<< HEAD
    /// Creates a new draft quote.
    /// </summary>
    /// <param name="clientId">FK to the client being quoted.</param>
    /// <param name="subject">Quote subject/title.</param>
    /// <param name="expiryDate">Quote expiry date (UTC).</param>
    /// <param name="notes">Optional notes or terms.</param>
    /// <returns>A new <see cref="Quote"/> with <see cref="QuoteStatus.Draft"/> status.</returns>
    public static Quote Create(int clientId, string subject, DateTimeOffset expiryDate, string? notes = null)
=======
    /// Creates a new quote for a client.
    /// </summary>
    /// <param name="clientId">FK to the client receiving the quote.</param>
    /// <param name="subject">Quote subject / title.</param>
    /// <param name="stage">Initial lifecycle stage.</param>
    /// <param name="validUntil">Expiry date; null for no expiry.</param>
    /// <param name="proposalText">Proposal text displayed at top; null to omit.</param>
    /// <param name="customerNotes">Customer-facing footer notes; null to omit.</param>
    /// <param name="adminNotes">Private admin notes; null to omit.</param>
    /// <returns>A new <see cref="Quote"/>.</returns>
    public static Quote Create(
        int clientId,
        string subject,
        QuoteStage stage,
        DateTimeOffset? validUntil,
        string? proposalText,
        string? customerNotes,
        string? adminNotes)
>>>>>>> origin/main
    {
        return new Quote
        {
            ClientId = clientId,
            Subject = subject,
<<<<<<< HEAD
            Status = QuoteStatus.Draft,
            ExpiryDate = expiryDate,
            Notes = notes,
            Total = 0m,
            CreatedAt = DateTimeOffset.UtcNow,
=======
            Stage = stage,
            DateCreated = DateTimeOffset.UtcNow,
            ValidUntil = validUntil,
            ProposalText = proposalText,
            CustomerNotes = customerNotes,
            AdminNotes = adminNotes,
            SubTotal = 0m,
            Total = 0m,
>>>>>>> origin/main
        };
    }

    /// <summary>
<<<<<<< HEAD
    /// Adds a line item and recalculates <see cref="Total"/>.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in an editable status.</exception>
    public void AddItem(string description, decimal unitPrice, int quantity)
    {
        if (Status is not (QuoteStatus.Draft or QuoteStatus.Sent))
        {
            throw new InvalidOperationException($"Cannot add items to a quote with status {Status}.");
        }

        var item = QuoteItem.Create(description, unitPrice, quantity);
        _items.Add(item);
        Total += item.Amount;
    }

    /// <summary>
    /// Sends the quote to the client (Draft → Sent).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in Draft status.</exception>
    public void Send()
    {
        if (Status != QuoteStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft quotes can be sent; current status is {Status}.");
        }

        Status = QuoteStatus.Sent;
    }

    /// <summary>
    /// Marks the quote as accepted by the client.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in a receivable status.</exception>
    public void Accept()
    {
        if (Status is not (QuoteStatus.Draft or QuoteStatus.Sent))
        {
            throw new InvalidOperationException($"Only Draft or Sent quotes can be accepted; current status is {Status}.");
        }

        Status = QuoteStatus.Accepted;
    }

    /// <summary>
    /// Marks the quote as declined by the client.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in a receivable status.</exception>
    public void Decline()
    {
        if (Status is QuoteStatus.Accepted or QuoteStatus.Declined or QuoteStatus.Expired or QuoteStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot decline a quote with status {Status}.");
        }

        Status = QuoteStatus.Declined;
    }

    /// <summary>
    /// Marks the quote as expired.
    /// </summary>
    public void MarkExpired()
    {
        if (Status is not (QuoteStatus.Draft or QuoteStatus.Sent))
        {
            throw new InvalidOperationException($"Only Draft or Sent quotes can be marked expired; current status is {Status}.");
        }

        Status = QuoteStatus.Expired;
    }

    /// <summary>
    /// Cancels the quote so it will not be pursued.
    /// </summary>
    public void Cancel()
    {
        if (Status is QuoteStatus.Accepted or QuoteStatus.Declined or QuoteStatus.Expired or QuoteStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel a quote with status {Status}.");
        }

        Status = QuoteStatus.Cancelled;
    }

    /// <summary>
    /// Updates the quote's mutable details.
    /// </summary>
    public void UpdateDetails(string subject, QuoteStatus status, DateTimeOffset expiryDate, string? notes)
    {
        Subject = subject;
        Status = status;
        ExpiryDate = expiryDate;
        Notes = notes;
    }

    /// <summary>
    /// Removes a line item by ID and recalculates <see cref="Total"/>.
    /// </summary>
    public void RemoveItem(int itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Quote item {itemId} not found.");
        Total -= item.Amount;
        _items.Remove(item);
    }

    /// <summary>
    /// Updates an existing line item and recalculates <see cref="Total"/>.
    /// </summary>
    public void UpdateItem(int itemId, string description, decimal unitPrice, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Quote item {itemId} not found.");
        Total -= item.Amount;
        item.Update(description, unitPrice, quantity);
        Total += item.Amount;
    }

    /// <summary>
    /// Creates a new Draft copy of this quote (with all its line items).
    /// </summary>
    public Quote Duplicate()
    {
        var copy = Quote.Create(ClientId, Subject, ExpiryDate, Notes);
        foreach (var item in _items)
        {
            copy.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }
=======
    /// Updates the quote details (subject, stage, dates, notes).
    /// </summary>
    /// <param name="subject">New quote subject / title.</param>
    /// <param name="stage">New lifecycle stage.</param>
    /// <param name="validUntil">New expiry date; null for no expiry.</param>
    /// <param name="proposalText">New proposal text; null to clear.</param>
    /// <param name="customerNotes">New customer notes; null to clear.</param>
    /// <param name="adminNotes">New admin notes; null to clear.</param>
    public void UpdateDetails(
        string subject,
        QuoteStage stage,
        DateTimeOffset? validUntil,
        string? proposalText,
        string? customerNotes,
        string? adminNotes)
    {
        Subject = subject;
        Stage = stage;
        ValidUntil = validUntil;
        ProposalText = proposalText;
        CustomerNotes = customerNotes;
        AdminNotes = adminNotes;
    }

    /// <summary>
    /// Adds a line item and recalculates totals.
    /// </summary>
    /// <param name="quantity">Number of units (>= 1).</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="unitPrice">Price per unit (>= 0).</param>
    /// <param name="discountPercent">Discount percentage (0–100).</param>
    /// <param name="taxed">Whether the item is subject to tax.</param>
    public void AddItem(int quantity, string description, decimal unitPrice, decimal discountPercent, bool taxed)
    {
        var item = QuoteItem.Create(quantity, description, unitPrice, discountPercent, taxed);
        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a line item by ID and recalculates totals.
    /// </summary>
    /// <param name="itemId">The ID of the item to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found on this quote.</exception>
    public void RemoveItem(int itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found on quote {Id}.");

        _items.Remove(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Updates an existing line item and recalculates totals.
    /// </summary>
    /// <param name="itemId">The ID of the item to update.</param>
    /// <param name="quantity">New number of units (>= 1).</param>
    /// <param name="description">New description.</param>
    /// <param name="unitPrice">New price per unit (>= 0).</param>
    /// <param name="discountPercent">New discount percentage (0–100).</param>
    /// <param name="taxed">Whether the item is subject to tax.</param>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found on this quote.</exception>
    public void UpdateItem(int itemId, int quantity, string description, decimal unitPrice, decimal discountPercent, bool taxed)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found on quote {Id}.");

        item.Update(quantity, description, unitPrice, discountPercent, taxed);
        RecalculateTotals();
    }

    /// <summary>
    /// Creates a duplicate of this quote as a new Draft, copying all line items.
    /// </summary>
    /// <returns>A new Draft <see cref="Quote"/> with copies of all line items.</returns>
    public Quote Duplicate()
    {
        var copy = Create(ClientId, Subject, QuoteStage.Draft, ValidUntil, ProposalText, CustomerNotes, AdminNotes);

        foreach (var item in _items)
        {
            copy.AddItem(item.Quantity, item.Description, item.UnitPrice, item.DiscountPercent, item.Taxed);
        }

>>>>>>> origin/main
        return copy;
    }

    /// <summary>
<<<<<<< HEAD
    /// Returns the line item data needed to generate an invoice.
    /// </summary>
    public IReadOnlyList<(string Description, decimal UnitPrice, int Quantity)> GetInvoiceItemData()
        => _items.Select(i => (i.Description, i.UnitPrice, i.Quantity)).ToList();
=======
    /// Returns the line item data needed to create an invoice from this quote.
    /// Does not create the invoice itself — the caller (application layer) is responsible.
    /// </summary>
    /// <returns>A list of tuples containing (Description, UnitPrice, Quantity) for each item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quote has no items.</exception>
    public IReadOnlyList<(string Description, decimal UnitPrice, int Quantity)> GetInvoiceItemData()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException($"Quote {Id} has no items to convert.");
        }

        return _items
            .Select(i => (i.Description, i.UnitPrice, i.Quantity))
            .ToList();
    }

    /// <summary>Recalculates <see cref="SubTotal"/> and <see cref="Total"/> from current items.</summary>
    private void RecalculateTotals()
    {
        SubTotal = _items.Sum(i => i.Amount);
        Total = SubTotal;
    }
>>>>>>> origin/main
}
