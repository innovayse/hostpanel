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

/// <summary>Total record counts for all entity types.</summary>
/// <param name="Clients">Total number of clients.</param>
/// <param name="Invoices">Total number of invoices.</param>
/// <param name="Services">Total number of services.</param>
/// <param name="Domains">Total number of domains.</param>
/// <param name="Tickets">Total number of tickets.</param>
/// <param name="Products">Total number of products.</param>
/// <param name="Orders">Total number of orders.</param>
/// <param name="Transactions">Total number of transactions.</param>
/// <param name="Quotes">Total number of quotes.</param>
/// <param name="Knowledgebase">Total number of knowledgebase articles.</param>
/// <param name="Contacts">Total number of contacts.</param>
/// <param name="TicketReplies">Total number of ticket replies.</param>
public sealed record MigrationTotalsDto(
    int Clients,
    int Invoices,
    int Services,
    int Domains,
    int Tickets,
    int Products = 0,
    int Orders = 0,
    int Transactions = 0,
    int Quotes = 0,
    int Knowledgebase = 0,
    int Contacts = 0,
    int TicketReplies = 0);

// ── Record shapes ─────────────────────────────────────────────────────────────

/// <summary>A single client record.</summary>
/// <param name="Email">Client email address.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="Company">Company name, if any.</param>
/// <param name="Phone">Phone number, if any.</param>
/// <param name="Address">Street address, if any.</param>
/// <param name="City">City, if any.</param>
/// <param name="State">State or region, if any.</param>
/// <param name="PostCode">Postal code, if any.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code, if any.</param>
/// <param name="Status">Client status string.</param>
public sealed record MigrationClientRecord(
    string Email, string FirstName, string LastName,
    string? Company, string? Phone,
    string? Address, string? City, string? State, string? PostCode, string? Country,
    string Status);

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

/// <summary>A single invoice line item.</summary>
/// <param name="Description">Item description.</param>
/// <param name="Amount">Unit amount.</param>
/// <param name="Quantity">Quantity.</param>
public sealed record MigrationInvoiceItemRecord(string Description, decimal Amount, int Quantity);

/// <summary>A single hosting service record.</summary>
/// <param name="ClientEmail">Email of the client this service belongs to.</param>
/// <param name="ProductName">Product/package name.</param>
/// <param name="BillingCycle">Billing cycle (monthly, annual, etc.).</param>
/// <param name="Status">Service status string.</param>
public sealed record MigrationServiceRecord(string ClientEmail, string ProductName, string BillingCycle, string Status);

/// <summary>A single domain record.</summary>
/// <param name="ClientEmail">Email of the client who owns the domain.</param>
/// <param name="DomainName">Fully-qualified domain name.</param>
/// <param name="RegisteredAt">Domain registration date.</param>
/// <param name="ExpiresAt">Domain expiry date.</param>
public sealed record MigrationDomainRecord(
    string ClientEmail, string DomainName, DateTimeOffset RegisteredAt, DateTimeOffset ExpiresAt);

/// <summary>A single support ticket record.</summary>
/// <param name="ClientEmail">Email of the client who opened the ticket.</param>
/// <param name="Subject">Ticket subject.</param>
/// <param name="Message">Opening message body.</param>
/// <param name="Status">Ticket status string.</param>
/// <param name="Priority">Ticket priority string.</param>
/// <param name="CreatedAt">When the ticket was created.</param>
public sealed record MigrationTicketRecord(
    string ClientEmail, string Subject, string Message,
    string Status, string Priority, DateTimeOffset CreatedAt);
