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
