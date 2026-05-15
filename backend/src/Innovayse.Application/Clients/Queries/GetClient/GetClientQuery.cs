namespace Innovayse.Application.Clients.Queries.GetClient;

/// <summary>Query to retrieve full details for a single client by primary key.</summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetClientQuery(int ClientId);
