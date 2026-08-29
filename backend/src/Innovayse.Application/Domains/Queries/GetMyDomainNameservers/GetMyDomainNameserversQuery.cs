namespace Innovayse.Application.Domains.Queries.GetMyDomainNameservers;

/// <summary>Query to retrieve the nameservers of one of the calling client's own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can. The admin route reads the whole domain through
/// <c>GetDomainQuery</c> and picks the list off it.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
public sealed record GetMyDomainNameserversQuery(int DomainId);
