namespace Innovayse.Application.Migration.Commands.ImportBatch;

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
