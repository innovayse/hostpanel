namespace Innovayse.Domain.Billing;

using Innovayse.Domain.Billing.Events;
using Innovayse.Domain.Common;

/// <summary>
/// Represents a charge that can be invoiced to a client.
/// Supports one-time, recurring, and time-billing scenarios.
/// Stored in the <c>billable_items</c> table.
/// </summary>
public sealed class BillableItem : AggregateRoot
{
    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the optional FK to a client service this charge relates to.</summary>
    public int? ServiceId { get; private set; }

    /// <summary>Gets the human-readable charge description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the total charge amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Gets the hours or quantity value.</summary>
    public decimal HoursQty { get; private set; }

    /// <summary>Gets a value indicating whether the item represents hours (true) or quantity (false).</summary>
    public bool IsHours { get; private set; }

    /// <summary>Gets the invoice action that determines how and when this item is invoiced.</summary>
    public InvoiceAction InvoiceAction { get; private set; }

    /// <summary>Gets the date when this item is due (UTC).</summary>
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>Gets the FK to the invoice this item was invoiced on; null if uninvoiced.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the number of times this item has been invoiced.</summary>
    public int InvoiceCount { get; private set; }

    /// <summary>Gets the recurrence interval (every N periods); null if non-recurring.</summary>
    public int? RecurrenceInterval { get; private set; }

    /// <summary>Gets the recurrence period unit; null if non-recurring.</summary>
    public RecurrencePeriod? RecurrencePeriod { get; private set; }

    /// <summary>Gets the maximum number of recurrences; null means unlimited.</summary>
    public int? RecurrenceLimit { get; private set; }

    /// <summary>Gets the UTC timestamp when this item was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private BillableItem() : base(0) { }

    /// <summary>
    /// Creates a new billable item for a client.
    /// </summary>
    /// <param name="clientId">FK to the client being charged.</param>
    /// <param name="serviceId">Optional FK to the related client service.</param>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="amount">Total charge amount.</param>
    /// <param name="hoursQty">Hours or quantity value.</param>
    /// <param name="isHours">True if this represents hours, false for quantity.</param>
    /// <param name="invoiceAction">How and when this item should be invoiced.</param>
    /// <param name="dueDate">When this item is due (UTC).</param>
    /// <param name="recurrenceInterval">Recurrence interval (every N periods); null if non-recurring.</param>
    /// <param name="recurrencePeriod">Recurrence period unit; null if non-recurring.</param>
    /// <param name="recurrenceLimit">Maximum recurrences; null means unlimited.</param>
    /// <returns>A new <see cref="BillableItem"/>.</returns>
    public static BillableItem Create(
        int clientId,
        int? serviceId,
        string description,
        decimal amount,
        decimal hoursQty,
        bool isHours,
        InvoiceAction invoiceAction,
        DateTimeOffset dueDate,
        int? recurrenceInterval,
        RecurrencePeriod? recurrencePeriod,
        int? recurrenceLimit)
    {
        return new BillableItem
        {
            ClientId = clientId,
            ServiceId = serviceId,
            Description = description,
            Amount = amount,
            HoursQty = hoursQty,
            IsHours = isHours,
            InvoiceAction = invoiceAction,
            DueDate = dueDate,
            RecurrenceInterval = recurrenceInterval,
            RecurrencePeriod = recurrencePeriod,
            RecurrenceLimit = recurrenceLimit,
            InvoiceCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Marks this item as invoiced on the given invoice and increments the invoice count.
    /// Raises <see cref="BillableItemInvoicedEvent"/>.
    /// </summary>
    /// <param name="invoiceId">The invoice ID this item was added to.</param>
    /// <exception cref="InvalidOperationException">Thrown when the item is already invoiced.</exception>
    public void MarkInvoiced(int invoiceId)
    {
        if (InvoiceId is not null)
        {
            throw new InvalidOperationException($"Billable item {Id} is already invoiced on invoice {InvoiceId}.");
        }

        InvoiceId = invoiceId;
        InvoiceCount++;
        AddDomainEvent(new BillableItemInvoicedEvent(Id, invoiceId, ClientId));
    }

    /// <summary>
    /// Advances the due date by the recurrence interval for the next billing cycle.
    /// Resets <see cref="InvoiceId"/> to null so the item can be invoiced again.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the item is not a recurring item.</exception>
    public void AdvanceDueDate()
    {
        if (RecurrenceInterval is null || RecurrencePeriod is null)
        {
            throw new InvalidOperationException($"Billable item {Id} is not a recurring item.");
        }

        DueDate = RecurrencePeriod.Value switch
        {
            Billing.RecurrencePeriod.Days => DueDate.AddDays(RecurrenceInterval.Value),
            Billing.RecurrencePeriod.Weeks => DueDate.AddDays(RecurrenceInterval.Value * 7),
            Billing.RecurrencePeriod.Months => DueDate.AddMonths(RecurrenceInterval.Value),
            Billing.RecurrencePeriod.Years => DueDate.AddYears(RecurrenceInterval.Value),
            _ => throw new InvalidOperationException($"Unknown recurrence period: {RecurrencePeriod.Value}"),
        };

        InvoiceId = null;
    }
}
