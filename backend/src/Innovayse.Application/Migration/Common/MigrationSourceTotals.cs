namespace Innovayse.Application.Migration.Common;

/// <summary>
/// How many records of each kind the legacy source holds, as it reports them before a pull
/// starts. The job uses these to size its progress counters, so a count here is the
/// denominator the admin UI shows — not a guarantee that every record will import.
/// </summary>
/// <param name="Clients">Client accounts available to pull.</param>
/// <param name="Invoices">Invoices available to pull.</param>
/// <param name="Services">Provisioned client services available to pull.</param>
/// <param name="Domains">Registered domains available to pull.</param>
/// <param name="Tickets">Support tickets available to pull.</param>
/// <param name="Products">Products available to pull.</param>
/// <param name="Orders">Orders available to pull.</param>
/// <param name="Transactions">Payment transactions available to pull.</param>
/// <param name="Quotes">Quotes available to pull.</param>
/// <param name="Knowledgebase">Knowledgebase articles available to pull.</param>
/// <param name="Contacts">Client sub-contacts available to pull.</param>
/// <param name="TicketReplies">Replies on support tickets available to pull.</param>
/// <param name="Announcements">Announcements available to pull.</param>
/// <param name="Downloads">Downloads available to pull.</param>
/// <param name="DownloadCategories">
/// Download categories available to pull. Reported by the source and carried here for
/// completeness; downloads are counted against <paramref name="Downloads"/> alone.
/// </param>
/// <param name="NetworkIssues">Network issues available to pull.</param>
public sealed record MigrationSourceTotals(
    int Clients,
    int Invoices,
    int Services,
    int Domains,
    int Tickets,
    int Products,
    int Orders,
    int Transactions,
    int Quotes,
    int Knowledgebase,
    int Contacts,
    int TicketReplies,
    int Announcements,
    int Downloads,
    int DownloadCategories,
    int NetworkIssues);
