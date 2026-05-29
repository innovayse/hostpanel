namespace Innovayse.Application.Billing.DTOs;

<<<<<<< HEAD
using Innovayse.Domain.Billing;

/// <summary>DTO for a billable item in admin lists with client name.</summary>
/// <param name="Id">BillableItem primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Amount">Unit price.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Type">OneTime or Recurring.</param>
/// <param name="RecurringPeriod">Recurring period (null for one-time).</param>
/// <param name="IsInvoiced">Whether the item has been invoiced.</param>
/// <param name="InvoiceId">FK to the invoice (null if not invoiced).</param>
/// <param name="NextDueDate">Next due date for recurring items.</param>
=======
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
>>>>>>> origin/main
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
public record BillableItemDto(
    int Id,
    int ClientId,
<<<<<<< HEAD
    string ClientName,
    string Description,
    decimal Amount,
    string Currency,
    string Type,
    string? RecurringPeriod,
    bool IsInvoiced,
    int? InvoiceId,
    DateTimeOffset? NextDueDate,
=======
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
>>>>>>> origin/main
    DateTimeOffset CreatedAt);
