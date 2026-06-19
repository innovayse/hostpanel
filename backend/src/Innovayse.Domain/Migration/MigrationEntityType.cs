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
    /// <summary>Products and product groups.</summary>
    Products,
    /// <summary>Customer orders.</summary>
    Orders,
    /// <summary>Payment transactions.</summary>
    Transactions,
    /// <summary>Sales quotes.</summary>
    Quotes,
    /// <summary>Knowledgebase articles.</summary>
    Knowledgebase,
    /// <summary>Client contacts.</summary>
    Contacts,
    /// <summary>Ticket replies.</summary>
    TicketReplies,
    /// <summary>Announcements.</summary>
    Announcements,
    /// <summary>Downloadable files.</summary>
    Downloads,
    /// <summary>Network status issues.</summary>
    NetworkIssues,
}
