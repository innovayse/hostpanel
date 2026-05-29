namespace Innovayse.Application.Billing.DTOs;

/// <summary>DTO representing a billable item.</summary>
/// <param name="Id">Billable item primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ServiceId">Optional FK to the related client service.</param>
/// <param name="ServiceName">Display name of the related service; null if no service linked.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="Amount">Total charge amount.</param>
/// <param name="HoursQty">Hours or quantity value.</param>
/// <param name="IsHours">True if this represents hours, false for quantity.</param>
/// <param name="InvoiceAction">Invoice action as a string for JSON serialization.</param>
/// <param name="DueDate">When this item is due (UTC).</param>
/// <param name="InvoiceId">FK to invoice; null if uninvoiced.</param>
/// <param name="InvoiceCount">Number of times this item has been invoiced.</param>
/// <param name="RecurrenceInterval">Recurrence interval; null if non-recurring.</param>
/// <param name="RecurrencePeriod">Recurrence period as a string; null if non-recurring.</param>
/// <param name="RecurrenceLimit">Maximum recurrences; null means unlimited.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
public record BillableItemDto(
    int Id,
    int ClientId,
    int? ServiceId,
    string? ServiceName,
    string Description,
    decimal Amount,
    decimal HoursQty,
    bool IsHours,
    string InvoiceAction,
    DateTimeOffset DueDate,
    int? InvoiceId,
    int InvoiceCount,
    int? RecurrenceInterval,
    string? RecurrencePeriod,
    int? RecurrenceLimit,
    DateTimeOffset CreatedAt);
