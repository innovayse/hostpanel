namespace Innovayse.Application.Billing.Commands.CreateTimeBillingEntries;

/// <summary>Command to create multiple time billing entries as billable items for a client.</summary>
/// <param name="ClientId">FK to the client being charged.</param>
/// <param name="Entries">Time billing entries to create.</param>
public record CreateTimeBillingEntriesCommand(int ClientId, IReadOnlyList<TimeBillingEntry> Entries);
