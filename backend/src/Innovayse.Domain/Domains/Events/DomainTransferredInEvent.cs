namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when an incoming domain transfer completes successfully.</summary>
/// <param name="DomainId">The domain ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="Name">The transferred domain name.</param>
public record DomainTransferredInEvent(int DomainId, int ClientId, string Name) : IDomainEvent;
