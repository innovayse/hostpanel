namespace Innovayse.Application.Services.Queries.GetMyServices;

/// <summary>Returns all services belonging to a specific client.</summary>
/// <param name="ClientId">The client's primary key.</param>
public record GetMyServicesQuery(int ClientId);
