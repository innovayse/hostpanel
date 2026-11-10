namespace Innovayse.Hostpanel.Domain.Migration.Enums;

/// <summary>Entity types that can be imported during a migration.</summary>
public enum MigrationEntityType
{
    /// <summary>Client accounts.</summary>
    Clients = 0,

    /// <summary>Billing invoices.</summary>
    Invoices = 1,

    /// <summary>Hosting services.</summary>
    Services = 2,

    /// <summary>Domain registrations.</summary>
    Domains = 3,

    /// <summary>Support tickets.</summary>
    Tickets = 4,

    /// <summary>Products and product groups.</summary>
    Products = 5,

    /// <summary>Customer orders.</summary>
    Orders = 6,

    /// <summary>Payment transactions.</summary>
    Transactions = 7,

    /// <summary>Sales quotes.</summary>
    Quotes = 8,

    /// <summary>Knowledgebase articles.</summary>
    Knowledgebase = 9,

    /// <summary>Client contacts.</summary>
    Contacts = 10,

    /// <summary>Ticket replies.</summary>
    TicketReplies = 11,

    /// <summary>Announcements.</summary>
    Announcements = 12,

    /// <summary>Downloadable files.</summary>
    Downloads = 13,

    /// <summary>Network status issues.</summary>
    NetworkIssues = 14,
}
