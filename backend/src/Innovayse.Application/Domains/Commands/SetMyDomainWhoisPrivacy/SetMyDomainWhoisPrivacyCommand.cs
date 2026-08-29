namespace Innovayse.Application.Domains.Commands.SetMyDomainWhoisPrivacy;

/// <summary>Command for a client to enable or disable WHOIS privacy on one of their own domains.</summary>
/// <remarks>
/// Carries a domain id but no client id. Which account the domain must belong to is resolved inside
/// the handler from the credential, so the scoping cannot be separated from the message the way an
/// id in the body can. The admin route that may act on any client's domain dispatches
/// <c>SetWhoisPrivacyCommand</c> directly.
/// </remarks>
/// <param name="DomainId">Primary key of the domain, which must belong to the caller.</param>
/// <param name="Value">The desired WHOIS privacy flag value.</param>
public sealed record SetMyDomainWhoisPrivacyCommand(
    int DomainId,
    bool Value);
