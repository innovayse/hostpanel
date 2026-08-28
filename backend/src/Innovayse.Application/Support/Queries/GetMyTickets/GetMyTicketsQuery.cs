namespace Innovayse.Application.Support.Queries.GetMyTickets;

/// <summary>Query to retrieve every ticket belonging to the calling client.</summary>
/// <remarks>
/// Carries no client id. Which account is resolved inside the handler from the credential,
/// so the scoping cannot be separated from the message the way an id in the body can.
/// </remarks>
public record GetMyTicketsQuery();
