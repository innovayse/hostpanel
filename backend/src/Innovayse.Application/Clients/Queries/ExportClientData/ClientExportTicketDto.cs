namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Support;

/// <summary>One support ticket, as the <c>tickets</c> section of a client data export lists it.</summary>
/// <param name="Id">The ticket's primary key.</param>
/// <param name="Subject">Ticket subject line.</param>
/// <param name="Status">Ticket status — Open, Answered, Closed and so on.</param>
/// <param name="Priority">Ticket priority.</param>
/// <param name="CreatedAt">When the ticket was opened.</param>
public sealed record ClientExportTicketDto(
    int Id,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt);
