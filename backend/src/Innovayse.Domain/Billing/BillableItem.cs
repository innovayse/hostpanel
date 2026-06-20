namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Common;

/// <summary>
/// A billable item — a product or service that can be added to an invoice.
/// Can be one-time or recurring, invoiced or waiting to be invoiced (Uninvoiced Items).
/// Stored in the <c>billable_items</c> table.
/// </summary>
public sealed class BillableItem : AggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets a human-readable description of the item.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>Gets the unit price.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the currency code (e.g. USD, EUR).</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Gets the item type (OneTime or Recurring).</summary>
    public BillableItemType Type { get; private set; }

    /// <summary>Gets the recurring period for recurring items (e.g. Monthly, Quarterly, Annual); null for one-time.</summary>
    public string? RecurringPeriod { get; private set; }

    /// <summary>Gets whether this item has been invoiced (added to an invoice).</summary>
    public bool IsInvoiced { get; private set; }

    /// <summary>Gets the FK to the invoice if invoiced; null otherwise.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the next date when this recurring item is due for invoicing.</summary>
    public DateTimeOffset? NextDueDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the item was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private BillableItem() : base(0) { }

    /// <summary>
    /// Creates a new billable item.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="amount">Unit price.</param>
    /// <param name="currency">Currency code.</param>
    /// <param name="type">OneTime or Recurring.</param>
    /// <param name="recurringPeriod">Recurring period (null for one-time).</param>
    /// <param name="nextDueDate">Next due date for recurring items (null for one-time).</param>
    /// <returns>A new billable item aggregate.</returns>
    public static BillableItem Create(
        int clientId,
        string description,
        decimal amount,
        string currency,
        BillableItemType type,
        string? recurringPeriod = null,
        DateTimeOffset? nextDueDate = null)
    {
        return new BillableItem
        {
            ClientId = clientId,
            Description = description,
            Amount = amount,
            Currency = currency,
            Type = type,
            RecurringPeriod = recurringPeriod,
            NextDueDate = nextDueDate,
            IsInvoiced = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Marks this item as invoiced and associates it with an invoice.
    /// </summary>
    /// <param name="invoiceId">The invoice ID.</param>
    public void MarkInvoiced(int invoiceId)
    {
        IsInvoiced = true;
        InvoiceId = invoiceId;
    }

    /// <summary>
    /// Updates the next due date for recurring items.
    /// </summary>
    /// <param name="nextDueDate">The new next due date.</param>
    public void UpdateNextDueDate(DateTimeOffset nextDueDate)
    {
        NextDueDate = nextDueDate;
    }

    /// <summary>
    /// Advances <see cref="NextDueDate"/> to the next recurrence based on <see cref="RecurringPeriod"/>.
    /// Resets <see cref="IsInvoiced"/> so the item is eligible for invoicing again.
    /// </summary>
    public void AdvanceDueDate()
    {
        if (NextDueDate is null)
        {
            return;
        }

        NextDueDate = RecurringPeriod?.ToLowerInvariant() switch
        {
            "monthly" => NextDueDate.Value.AddMonths(1),
            "quarterly" => NextDueDate.Value.AddMonths(3),
            "annually" or "yearly" => NextDueDate.Value.AddYears(1),
            "weekly" => NextDueDate.Value.AddDays(7),
            _ => NextDueDate.Value.AddMonths(1),
        };

        IsInvoiced = false;
        InvoiceId = null;
    }
}
