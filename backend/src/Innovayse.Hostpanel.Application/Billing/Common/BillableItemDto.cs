using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;
/// <summary>DTO for a billable item in admin lists with client name.</summary>
/// <param name="Id">BillableItem primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Full name of the owning client; every billable item has one, since its client cannot be deleted.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Amount">Unit price.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Type">Item type.</param>
/// <param name="RecurringPeriod">Recurring period (null for one-time).</param>
/// <param name="IsInvoiced">Whether the item has been invoiced.</param>
/// <param name="InvoiceId">FK to the invoice (null if not invoiced).</param>
/// <param name="NextDueDate">Day the item is invoiced automatically (once for a one-time item, next for a recurring one), null for none.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
public sealed record BillableItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string Description,
    decimal Amount,
    string Currency,
    BillableItemType Type,
    string? RecurringPeriod,
    bool IsInvoiced,
    int? InvoiceId,
    DateTimeOffset? NextDueDate,
    DateTimeOffset CreatedAt);
