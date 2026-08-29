namespace Innovayse.Application.Domains.Queries.GetMyDomain;

/// <summary>Query to retrieve one of the calling client's own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>GetDomainQuery</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
public sealed record GetMyDomainQuery(int DomainId);
