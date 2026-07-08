namespace Innovayse.Domain.Email.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when an email domain passes DNS verification and becomes active.</summary>
/// <param name="EmailDomainId">The email domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="DomainName">The email domain name (e.g. "example.com").</param>
public record EmailDomainActivatedEvent(int EmailDomainId, int ClientId, string DomainName) : IDomainEvent;
