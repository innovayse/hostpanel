namespace Innovayse.Application.Billing.DTOs;

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
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
public record BillableItemDto(
    int Id,
    int ClientId,
    string ClientName,
    string Description,
    decimal Amount,
    string Currency,
    string Type,
    string? RecurringPeriod,
    bool IsInvoiced,
    int? InvoiceId,
    DateTimeOffset? NextDueDate,
    DateTimeOffset CreatedAt);
