namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single invoice record.</summary>
/// <param name="ClientEmail">Email of the client this invoice belongs to.</param>
/// <param name="Total">Invoice total amount.</param>
/// <param name="Status">Invoice status string.</param>
/// <param name="Date">Invoice date.</param>
/// <param name="DueDate">Invoice due date.</param>
/// <param name="Items">Line items on the invoice.</param>
public sealed record MigrationInvoiceRecord(
    string ClientEmail, decimal Total, string Status,
    DateTimeOffset Date, DateTimeOffset DueDate,
    IReadOnlyList<MigrationInvoiceItemRecord> Items);
