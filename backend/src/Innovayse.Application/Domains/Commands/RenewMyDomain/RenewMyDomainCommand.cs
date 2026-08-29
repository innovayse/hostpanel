namespace Innovayse.Application.Domains.Commands.RenewMyDomain;

/// <summary>Command for a client to renew one of their own domain registrations.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>RenewDomainCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Years">Number of years to extend the registration (1-10).</param>
public sealed record RenewMyDomainCommand(
    int DomainId,
    int Years);
