namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A quotation (estimate) issued to a client for products or services.
/// Owns a collection of <see cref="QuoteItem"/> line items.
/// Can be accepted by the client and converted to an invoice.
/// Stored in the <c>quotes</c> table.
/// </summary>
public sealed class Quote : AggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<QuoteItem> _items = [];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the quote subject/title.</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>Gets the current lifecycle stage.</summary>
    public QuoteStage Stage { get; private set; }

    /// <summary>Gets the quote expiry date (UTC).</summary>
    public DateTimeOffset ExpiryDate { get; private set; }

    /// <summary>Gets optional notes or terms for the quote.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the proposal text displayed at the top of the quote.</summary>
    public string? ProposalText { get; private set; }

    /// <summary>Gets the customer-facing notes displayed as a footer.</summary>
    public string? CustomerNotes { get; private set; }

    /// <summary>Gets the admin-only internal notes (not visible to client).</summary>
    public string? AdminNotes { get; private set; }

    /// <summary>Gets the running total of all line items.</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets the UTC timestamp when the quote was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the read-only view of quote line items.</summary>
    public IReadOnlyList<QuoteItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Quote() : base(0) { }

    /// <summary>
    /// Creates a new draft quote.
    /// </summary>
    /// <param name="clientId">FK to the client being quoted.</param>
    /// <param name="subject">Quote subject/title.</param>
    /// <param name="expiryDate">Quote expiry date (UTC).</param>
    /// <param name="notes">Optional notes or terms.</param>
    /// <param name="proposalText">Optional proposal text shown at the top.</param>
    /// <param name="customerNotes">Optional customer-facing footer notes.</param>
    /// <param name="adminNotes">Optional admin-only internal notes.</param>
    /// <returns>A new <see cref="Quote"/> with <see cref="QuoteStage.Draft"/> stage.</returns>
    public static Quote Create(int clientId, string subject, DateTimeOffset expiryDate, string? notes = null, string? proposalText = null, string? customerNotes = null, string? adminNotes = null)
    {
        return new Quote
        {
            ClientId = clientId,
            Subject = subject,
            Stage = QuoteStage.Draft,
            ExpiryDate = expiryDate,
            Notes = notes,
            ProposalText = proposalText,
            CustomerNotes = customerNotes,
            AdminNotes = adminNotes,
            Total = 0m,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Adds a line item and recalculates <see cref="Total"/>.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit (≥ 0).</param>
    /// <param name="quantity">Number of units (≥ 1).</param>
    /// <param name="discountPercent">Discount percentage (0–100). Defaults to 0.</param>
    /// <param name="taxed">Whether this item is taxed. Defaults to false.</param>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in an editable stage.</exception>
    public void AddItem(string description, decimal unitPrice, int quantity, decimal discountPercent = 0, bool taxed = false)
    {
        if (Stage is not (QuoteStage.Draft or QuoteStage.Delivered or QuoteStage.OnHold))
        {
            throw new InvalidOperationException($"Cannot add items to a quote with stage {Stage}.");
        }

        var item = QuoteItem.Create(description, unitPrice, quantity, discountPercent, taxed);
        _items.Add(item);
        Total += item.Amount;
    }

    /// <summary>
    /// Delivers the quote to the client (Draft → Delivered).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in Draft stage.</exception>
    public void Deliver()
    {
        if (Stage != QuoteStage.Draft)
        {
            throw new InvalidOperationException($"Only Draft quotes can be delivered; current stage is {Stage}.");
        }

        Stage = QuoteStage.Delivered;
    }

    /// <summary>
    /// Puts the quote on hold.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote cannot be put on hold.</exception>
    public void PutOnHold()
    {
        if (Stage is not (QuoteStage.Draft or QuoteStage.Delivered))
        {
            throw new InvalidOperationException($"Only Draft or Delivered quotes can be put on hold; current stage is {Stage}.");
        }

        Stage = QuoteStage.OnHold;
    }

    /// <summary>
    /// Marks the quote as accepted by the client.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not in an acceptable stage.</exception>
    public void Accept()
    {
        if (Stage is not (QuoteStage.Draft or QuoteStage.Delivered or QuoteStage.OnHold))
        {
            throw new InvalidOperationException($"Only Draft, Delivered, or OnHold quotes can be accepted; current stage is {Stage}.");
        }

        Stage = QuoteStage.Accepted;
    }

    /// <summary>
    /// Marks the quote as lost (declined by the client).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote cannot be marked as lost.</exception>
    public void MarkLost()
    {
        if (Stage is QuoteStage.Accepted or QuoteStage.Lost or QuoteStage.Expired or QuoteStage.Dead)
        {
            throw new InvalidOperationException($"Cannot mark a quote as lost with stage {Stage}.");
        }

        Stage = QuoteStage.Lost;
    }

    /// <summary>
    /// Marks the quote as expired.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote cannot be marked as expired.</exception>
    public void MarkExpired()
    {
        if (Stage is not (QuoteStage.Draft or QuoteStage.Delivered or QuoteStage.OnHold))
        {
            throw new InvalidOperationException($"Only Draft, Delivered, or OnHold quotes can be marked expired; current stage is {Stage}.");
        }

        Stage = QuoteStage.Expired;
    }

    /// <summary>
    /// Marks the quote as dead (permanently abandoned).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the quote cannot be marked as dead.</exception>
    public void MarkDead()
    {
        if (Stage is QuoteStage.Accepted or QuoteStage.Dead)
        {
            throw new InvalidOperationException($"Cannot mark a quote as dead with stage {Stage}.");
        }

        Stage = QuoteStage.Dead;
    }

    /// <summary>
    /// Updates the quote's mutable details.
    /// </summary>
    /// <param name="subject">New quote subject/title.</param>
    /// <param name="stage">New lifecycle stage.</param>
    /// <param name="expiryDate">New expiry date (UTC).</param>
    /// <param name="notes">Optional notes or terms; null to clear.</param>
    /// <param name="proposalText">Optional proposal text; null to clear.</param>
    /// <param name="customerNotes">Optional customer-facing notes; null to clear.</param>
    /// <param name="adminNotes">Optional admin-only notes; null to clear.</param>
    public void UpdateDetails(string subject, QuoteStage stage, DateTimeOffset expiryDate, string? notes, string? proposalText = null, string? customerNotes = null, string? adminNotes = null)
    {
        Subject = subject;
        Stage = stage;
        ExpiryDate = expiryDate;
        Notes = notes;
        ProposalText = proposalText;
        CustomerNotes = customerNotes;
        AdminNotes = adminNotes;
    }

    /// <summary>
    /// Removes a line item by ID and recalculates <see cref="Total"/>.
    /// </summary>
    /// <param name="itemId">The ID of the item to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the item is not found.</exception>
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
    /// <param name="itemId">The ID of the item to update.</param>
    /// <param name="description">New description.</param>
    /// <param name="unitPrice">New unit price.</param>
    /// <param name="quantity">New quantity.</param>
    /// <param name="discountPercent">New discount percentage (0–100).</param>
    /// <param name="taxed">Whether this item is taxed.</param>
    public void UpdateItem(int itemId, string description, decimal unitPrice, int quantity, decimal discountPercent = 0, bool taxed = false)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"Quote item {itemId} not found.");
        Total -= item.Amount;
        item.Update(description, unitPrice, quantity, discountPercent, taxed);
        Total += item.Amount;
    }

    /// <summary>
    /// Creates a new Draft copy of this quote (with all its line items).
    /// </summary>
    /// <returns>A new draft <see cref="Quote"/> with copies of all line items.</returns>
    public Quote Duplicate()
    {
        var copy = Quote.Create(ClientId, Subject, ExpiryDate, Notes, ProposalText, CustomerNotes, AdminNotes);
        foreach (var item in _items)
        {
            copy.AddItem(item.Description, item.UnitPrice, item.Quantity, item.DiscountPercent, item.Taxed);
        }
        return copy;
    }

    /// <summary>
    /// Returns the line item data needed to generate an invoice.
    /// </summary>
    /// <returns>Read-only list of item tuples (Description, UnitPrice, Quantity).</returns>
    public IReadOnlyList<(string Description, decimal UnitPrice, int Quantity)> GetInvoiceItemData()
        => _items.Select(i => (i.Description, i.UnitPrice, i.Quantity)).ToList();
}
