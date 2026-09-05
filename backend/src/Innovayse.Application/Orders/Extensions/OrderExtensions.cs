namespace Innovayse.Application.Orders.Extensions;

using Innovayse.Domain.Orders;

/// <summary>
/// Extension methods shared by the order handlers that are reachable without a credential.
/// </summary>
public static class OrderExtensions
{
    /// <summary>
    /// Returns the order when the caller presented its payment token, and refuses otherwise.
    /// </summary>
    /// <remarks>
    /// Every payment endpoint an anonymous checkout can reach goes through here, so the four of
    /// them cannot drift apart on what they accept.
    /// <para>
    /// A missing order and a wrong token are refused with the identical message on purpose. Order
    /// ids are sequential, so an error that distinguished "no such order" from "not your order"
    /// would let anyone map out which ids exist and how far the shop's order numbering has got.
    /// The caller learns only that this id and this token do not go together.
    /// </para>
    /// </remarks>
    /// <param name="order">The order that was looked up, or <see langword="null"/> when there was none.</param>
    /// <param name="orderId">The id that was looked up, used in the refusal message.</param>
    /// <param name="paymentToken">The token the caller presented, or null when none was sent.</param>
    /// <returns>The order, once the token has been verified against it.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no such order exists or the presented token is not that order's.
    /// </exception>
    public static Order EnsurePayableWith(this Order? order, int orderId, string? paymentToken)
    {
        if (order is null || !order.MatchesPaymentToken(paymentToken))
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        return order;
    }
}
