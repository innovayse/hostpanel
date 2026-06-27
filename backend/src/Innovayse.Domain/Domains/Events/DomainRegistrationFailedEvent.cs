namespace Innovayse.Domain.Domains.Events;

using Innovayse.Domain.Common;

/// <summary>
/// Raised when a domain registration or transfer order is rejected by the registrar
/// and the client has already been charged.
/// Used to trigger automatic refund and failure notifications.
/// </summary>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="DomainName">The domain name that failed to register.</param>
/// <param name="InvoiceId">The invoice ID that was paid and will be refunded.</param>
/// <param name="Reason">Human-readable reason for the failure (from the registrar).</param>
public record DomainRegistrationFailedEvent(
    int ClientId,
    string DomainName,
    int InvoiceId,
    string Reason) : IDomainEvent;
