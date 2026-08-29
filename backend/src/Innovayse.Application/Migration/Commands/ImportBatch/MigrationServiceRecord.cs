namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single hosting service record.</summary>
/// <param name="ClientEmail">Email of the client this service belongs to.</param>
/// <param name="ProductName">Product/package name.</param>
/// <param name="BillingCycle">Billing cycle (monthly, annual, etc.).</param>
/// <param name="Status">Service status string.</param>
public sealed record MigrationServiceRecord(string ClientEmail, string ProductName, string BillingCycle, string Status);
