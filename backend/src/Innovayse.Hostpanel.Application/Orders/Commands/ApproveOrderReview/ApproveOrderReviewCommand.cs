namespace Innovayse.Hostpanel.Application.Orders.Commands.ApproveOrderReview;

/// <summary>Approves an order that fraud detection held for review, so it is accepted and fulfilled the ordinary way.</summary>
/// <param name="OrderId">The order to approve; set from the route.</param>
public sealed record ApproveOrderReviewCommand(int OrderId);
