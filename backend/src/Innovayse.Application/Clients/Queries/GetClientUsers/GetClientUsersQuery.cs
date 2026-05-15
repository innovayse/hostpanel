namespace Innovayse.Application.Clients.Queries.GetClientUsers;

/// <summary>Query to retrieve all users linked to a client (owner + additional).</summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetClientUsersQuery(int ClientId);
