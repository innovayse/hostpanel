namespace Innovayse.Application.Domains.Commands.ModifyMyDomainContact;

using Innovayse.Domain.Domains;

/// <summary>Command for a client to modify the WHOIS registrant contact on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>ModifyDomainContactCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Contact">The updated registrant contact details.</param>
public sealed record ModifyMyDomainContactCommand(
    int DomainId,
    DomainContact Contact);
