namespace Innovayse.Hostpanel.Application.Clients.Queries.GetClientGroup;

/// <summary>Query to retrieve a single client group by id.</summary>
/// <param name="Id">The identifier of the group to retrieve.</param>
public sealed record GetClientGroupQuery(int Id);
