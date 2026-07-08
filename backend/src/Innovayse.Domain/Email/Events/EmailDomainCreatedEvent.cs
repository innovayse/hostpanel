namespace Innovayse.Domain.Email.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new email domain is created and pending DNS verification.</summary>
/// <param name="EmailDomainId">The email domain ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="DomainName">The email domain name (e.g. "example.com").</param>
public record EmailDomainCreatedEvent(int EmailDomainId, int ClientId, string DomainName) : IDomainEvent;
