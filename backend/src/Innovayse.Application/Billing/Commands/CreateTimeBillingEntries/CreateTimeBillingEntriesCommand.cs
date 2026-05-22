namespace Innovayse.Application.Billing.Commands.CreateTimeBillingEntries;

/// <summary>A single time billing entry to record.</summary>
/// <param name="ServiceId">Optional FK to the related client service.</param>
/// <param name="Description">Human-readable description of the time entry.</param>
/// <param name="Hours">Number of hours worked.</param>
/// <param name="Rate">Hourly rate to charge.</param>
public record TimeBillingEntry(int? ServiceId, string Description, decimal Hours, decimal Rate);

/// <summary>Command to create multiple time billing entries as billable items for a client.</summary>
/// <param name="ClientId">FK to the client being charged.</param>
/// <param name="Entries">Time billing entries to create.</param>
public record CreateTimeBillingEntriesCommand(int ClientId, IReadOnlyList<TimeBillingEntry> Entries);
