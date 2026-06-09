namespace Innovayse.API.Migration.Requests;

/// <summary>Request body for creating a new migration job.</summary>
public sealed record CreateMigrationJobRequest(
    string? Label,
    string SourceUrl,
    bool ExportClients = true,
    bool ExportInvoices = true,
    bool ExportServices = true,
    bool ExportDomains = true,
    bool ExportTickets = true);
