namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/clients/{clientId}/billable-items — creates a new billable item.</summary>
public sealed class CreateBillableItemRequest
{
    /// <summary>Gets or initialises the optional FK to the related client service.</summary>
    public int? ServiceId { get; init; }

    /// <summary>Gets or initialises the human-readable charge description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the total charge amount.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Gets or initialises the hours or quantity value.</summary>
    public decimal HoursQty { get; init; }

    /// <summary>Gets or initialises whether the item represents hours (true) or quantity (false).</summary>
    public bool IsHours { get; init; }

    /// <summary>Gets or initialises the invoice action (string, parsed to enum in controller).</summary>
    public required string InvoiceAction { get; init; }

    /// <summary>Gets or initialises the due date (UTC).</summary>
    public required DateTimeOffset DueDate { get; init; }

    /// <summary>Gets or initialises the recurrence interval; null if non-recurring.</summary>
    public int? RecurrenceInterval { get; init; }

    /// <summary>Gets or initialises the recurrence period (string); null if non-recurring.</summary>
    public string? RecurrencePeriod { get; init; }

    /// <summary>Gets or initialises the maximum recurrences; null means unlimited.</summary>
    public int? RecurrenceLimit { get; init; }
}
