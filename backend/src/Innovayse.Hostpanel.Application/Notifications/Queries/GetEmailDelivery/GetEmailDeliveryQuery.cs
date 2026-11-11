namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;

/// <summary>Query to read how the delivery of one keyed mail ended.</summary>
/// <param name="IdempotencyKey">The key the mail was sent with, per recipient (<c>EmailIdempotencyKey.ForRecipient</c>).</param>
public sealed record GetEmailDeliveryQuery(string IdempotencyKey);
