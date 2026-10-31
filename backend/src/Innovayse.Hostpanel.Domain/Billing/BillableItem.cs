using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A billable item: a product or service that can be added to an invoice.
/// Can be one-time or recurring, invoiced or waiting to be invoiced (Uninvoiced Items).
/// Stored in the <c>BillableItems</c> table.
/// </summary>
public sealed class BillableItem : BaseEntity, IAggregateRoot
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

    /// <summary>
    /// Gets the date the item is due for automatic invoicing: once for a one-time item, next for a
    /// recurring one; null when it is only invoiced by hand.
    /// </summary>
    public DateTimeOffset? NextDueDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the item was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private BillableItem() { }

    /// <summary>
    /// Creates a new billable item.
    /// </summary>
    /// <param name="clientId">FK to the client.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="amount">Unit price.</param>
    /// <param name="currency">Currency code, stored upper-cased.</param>
    /// <param name="type">OneTime or Recurring.</param>
    /// <param name="recurringPeriod">Recurring period (null for one-time).</param>
    /// <param name="nextDueDate">
    /// Day the item is invoiced automatically (once for a one-time item, next for a recurring one), or
    /// null to invoice it by hand.
    /// </param>
    /// <returns>A new billable item aggregate.</returns>
    /// <exception cref="ArgumentException">The currency is not three letters.</exception>
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
            Currency = CurrencyCode.Normalise(currency),
            Type = type,
            RecurringPeriod = recurringPeriod,
            NextDueDate = nextDueDate,
            IsInvoiced = false,
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
    /// Takes this item off the invoice it was put on, so it is waiting to be invoiced again: the
    /// invoice no longer exists, or no longer stands, and the item still has to be billed.
    /// </summary>
    public void ReleaseFromInvoice()
    {
        IsInvoiced = false;
        InvoiceId = null;
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
            RecurringPeriodNames.Monthly => NextDueDate.Value.AddMonths(1),
            RecurringPeriodNames.Quarterly => NextDueDate.Value.AddMonths(3),
            RecurringPeriodNames.Annually or RecurringPeriodNames.Yearly => NextDueDate.Value.AddYears(1),
            RecurringPeriodNames.Weekly => NextDueDate.Value.AddDays(7),
            _ => NextDueDate.Value.AddMonths(1),
        };

        ReleaseFromInvoice();
    }
}
