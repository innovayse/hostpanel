namespace Innovayse.Domain.Migration;

/// <summary>Entity types that can be imported during a migration.</summary>
public enum MigrationEntityType
{
    /// <summary>Client accounts.</summary>
    Clients,
    /// <summary>Billing invoices.</summary>
    Invoices,
    /// <summary>Hosting services.</summary>
    Services,
    /// <summary>Domain registrations.</summary>
    Domains,
    /// <summary>Support tickets.</summary>
    Tickets,
}
