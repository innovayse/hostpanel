namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single invoice line item.</summary>
/// <param name="Description">Item description.</param>
/// <param name="Amount">Unit amount.</param>
/// <param name="Quantity">Quantity.</param>
public sealed record MigrationInvoiceItemRecord(string Description, decimal Amount, int Quantity);
