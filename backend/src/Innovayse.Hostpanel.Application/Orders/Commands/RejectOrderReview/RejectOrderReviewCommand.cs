namespace Innovayse.Hostpanel.Application.Orders.Commands.RejectOrderReview;

/// <summary>Rejects an order that fraud detection held for review; the client is told why.</summary>
/// <param name="OrderId">The order to reject; set from the route.</param>
/// <param name="Reason">Why the order is refused. The client reads it in the e-mail.</param>
public sealed record RejectOrderReviewCommand(int OrderId, string Reason);
