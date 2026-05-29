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
    /// <returns>A new <see cref="Quote"/> with <see cref="QuoteStatus.Draft"/> status.</returns>
    public static Quote Create(int clientId, string subject, DateTimeOffset expiryDate, string? notes = null)
    {
        return new Quote
        {
            ClientId = clientId,
            Subject = subject,
            Status = QuoteStatus.Draft,
            ExpiryDate = expiryDate,
            Notes = notes,
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
}
