namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a domain registration is activated by the registrar.</summary>
/// <param name="DomainId">The domain ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="Name">The registered domain name.</param>
public record DomainRegisteredEvent(int DomainId, int ClientId, string Name) : IDomainEvent;
