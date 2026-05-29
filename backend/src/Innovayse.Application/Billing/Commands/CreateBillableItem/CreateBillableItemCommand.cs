namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using Innovayse.Domain.Billing;

/// <summary>Command to create a new billable item for a client.</summary>
/// <param name="ClientId">FK to the client being charged.</param>
/// <param name="ServiceId">Optional FK to the related client service.</param>
/// <param name="Description">Human-readable charge description.</param>
/// <param name="Amount">Total charge amount.</param>
/// <param name="HoursQty">Hours or quantity value.</param>
/// <param name="IsHours">True if this represents hours, false for quantity.</param>
/// <param name="InvoiceAction">How and when this item should be invoiced.</param>
/// <param name="DueDate">When this item is due (UTC).</param>
/// <param name="RecurrenceInterval">Recurrence interval; null if non-recurring.</param>
/// <param name="RecurrencePeriod">Recurrence period unit; null if non-recurring.</param>
/// <param name="RecurrenceLimit">Maximum recurrences; null means unlimited.</param>
public record CreateBillableItemCommand(
    int ClientId,
    int? ServiceId,
    string Description,
    decimal Amount,
    decimal HoursQty,
    bool IsHours,
    InvoiceAction InvoiceAction,
    DateTimeOffset DueDate,
    int? RecurrenceInterval,
    RecurrencePeriod? RecurrencePeriod,
    int? RecurrenceLimit);
