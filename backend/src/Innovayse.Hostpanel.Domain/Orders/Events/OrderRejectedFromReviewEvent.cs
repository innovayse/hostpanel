using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Orders.Events;

/// <summary>Raised when staff refuse an order that was held for fraud review; the client is told why.</summary>
/// <param name="OrderId">The refused order.</param>
/// <param name="ClientId">The client who placed it.</param>
/// <param name="OrderNumber">The order's number, for the client's mail.</param>
/// <param name="Reason">The reason staff gave, for the client's mail.</param>
public sealed record OrderRejectedFromReviewEvent(int OrderId, int ClientId, string OrderNumber, string Reason) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
