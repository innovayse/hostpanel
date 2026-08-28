namespace Innovayse.Application.Domains.Queries.GetMyDomains;

/// <summary>Query to retrieve every domain owned by the calling client.</summary>
/// <remarks>
/// Carries no user id. Whose domains is resolved inside the handler from the credential.
/// </remarks>
public record GetMyDomainsQuery();
