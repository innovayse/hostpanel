namespace Innovayse.Application.Reports.Queries.GetTicketTags;

/// <summary>Query for the Ticket Tags report.</summary>
/// <param name="From">Earliest ticket date to include.</param>
/// <param name="To">Latest ticket date to include.</param>
public record GetTicketTagsQuery(DateOnly? From = null, DateOnly? To = null);
