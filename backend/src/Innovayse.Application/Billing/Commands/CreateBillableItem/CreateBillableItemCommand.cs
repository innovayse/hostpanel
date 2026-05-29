namespace Innovayse.Application.Billing.Commands.CreateBillableItem;

using Innovayse.Application.Billing.DTOs;

/// <summary>Command to create a new billable item.</summary>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Amount">Unit price.</param>
/// <param name="Currency">Currency code (e.g. USD).</param>
/// <param name="Type">Item type (OneTime or Recurring).</param>
/// <param name="RecurringPeriod">Recurring period (Monthly, Quarterly, Annual) — required for Recurring.</param>
/// <param name="NextDueDate">Next due date — optional, for recurring items.</param>
public sealed record CreateBillableItemCommand(
    int ClientId,
    string Description,
    decimal Amount,
    string Currency,
    string Type,
    string? RecurringPeriod = null,
    DateTimeOffset? NextDueDate = null);
