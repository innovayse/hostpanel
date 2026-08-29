namespace Innovayse.Application.Migration.Commands.ImportBatch;

using Innovayse.Domain.Migration;

/// <summary>
/// Sent by the migration plugin for each batch of records.
/// On the first call (page 1 of Clients) the totals are set and the job transitions to InProgress.
/// </summary>
/// <param name="Key">Secret key that identifies the migration job.</param>
/// <param name="EntityType">Entity type being imported in this batch.</param>
/// <param name="Page">Current 1-based page number.</param>
/// <param name="TotalPages">Total number of pages for this entity type.</param>
/// <param name="Totals">Total record counts across all entity types — only required on the very first batch.</param>
/// <param name="Clients">Client records in this batch.</param>
/// <param name="Invoices">Invoice records in this batch.</param>
/// <param name="Services">Service records in this batch.</param>
/// <param name="Domains">Domain records in this batch.</param>
/// <param name="Tickets">Ticket records in this batch.</param>
public sealed record ImportBatchCommand(
    string Key,
    MigrationEntityType EntityType,
    int Page,
    int TotalPages,
    MigrationTotalsDto? Totals,
    IReadOnlyList<MigrationClientRecord>? Clients,
    IReadOnlyList<MigrationInvoiceRecord>? Invoices,
    IReadOnlyList<MigrationServiceRecord>? Services,
    IReadOnlyList<MigrationDomainRecord>? Domains,
    IReadOnlyList<MigrationTicketRecord>? Tickets);
