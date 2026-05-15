namespace Innovayse.Application.Domains.Queries.GetMyDomains;

/// <summary>Query to retrieve all domains owned by the authenticated client.</summary>
/// <param name="UserId">The authenticated user's Identity ID (used to resolve the client record).</param>
public record GetMyDomainsQuery(string UserId);
