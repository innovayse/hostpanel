namespace Innovayse.Application.Domains.Queries.GetMyDomainWhois;

/// <summary>Query to perform a WHOIS lookup on one of the calling client's own domains.</summary>
/// <remarks>
/// <para>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved
/// inside the handler from the credential, so the scoping cannot be separated from the message
/// the way an id in the body can.
/// </para>
/// <para>
/// It carries an id rather than a name for the same reason: <c>GetWhoisQuery</c> takes a
/// hostname, and a hostname is not something a client can be shown to own. The handler resolves
/// the id to a name only after ownership is settled, so the lookup cannot be pointed at an
/// arbitrary domain by putting one in the body.
/// </para>
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
public sealed record GetMyDomainWhoisQuery(int DomainId);
