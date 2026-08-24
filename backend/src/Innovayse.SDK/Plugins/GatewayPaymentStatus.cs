namespace Innovayse.SDK.Plugins;

/// <summary>Mapped state of a gateway payment.</summary>
public enum GatewayPaymentState
{
    /// <summary>The payment is not finished yet (session open, or authorization in progress).</summary>
    Pending,

    /// <summary>Funds were captured; the payment is complete.</summary>
    Paid,

    /// <summary>The payment was declined, cancelled, refunded, or the session is unknown to the gateway.</summary>
    Declined,
}

/// <summary>Result of <see cref="IPaymentPlugin.GetStatusAsync"/>.</summary>
/// <param name="State">The mapped payment state.</param>
/// <param name="TransactionId">Gateway transaction reference; set only when <paramref name="State"/> is <see cref="GatewayPaymentState.Paid"/>.</param>
/// <param name="RawStatus">The gateway's raw status value, for diagnostics.</param>
public sealed record GatewayPaymentStatus(
    GatewayPaymentState State,
    string? TransactionId,
    string? RawStatus);
