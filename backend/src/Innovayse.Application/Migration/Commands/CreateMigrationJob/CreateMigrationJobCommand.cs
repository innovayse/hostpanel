namespace Innovayse.Application.Migration.Commands.CreateMigrationJob;

/// <summary>Creates a new pending migration job and returns its ID and secret key.</summary>
public sealed record CreateMigrationJobCommand(
    string? Label,
    string SourceUrl,
    bool ExportClients,
    bool ExportInvoices,
    bool ExportServices,
    bool ExportDomains,
    bool ExportTickets);
