namespace Innovayse.API.Billing;

using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
/// Converts <see cref="GatewayCompletionState"/> to the lowercase wire string the client
/// contract expects (<c>client/pages/payment/result.vue</c> and the checkout flows compare
/// against these literals directly). The conversion happens only at this HTTP boundary — the
/// enum itself never leaves the backend.
/// </summary>
public static class GatewayCompletionStateWireFormat
{
    /// <summary>Converts a completion state to its wire string.</summary>
    /// <param name="state">The completion state to convert.</param>
    /// <returns>"paid", "pending", or "declined".</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unrecognized enum value.</exception>
    public static string ToWireString(this GatewayCompletionState state) => state switch
    {
        GatewayCompletionState.Paid => "paid",
        GatewayCompletionState.Pending => "pending",
        GatewayCompletionState.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unrecognized gateway completion state."),
    };
}
