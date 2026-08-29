namespace Innovayse.SDK.Plugins;

/// <summary>Result of <see cref="IPaymentPlugin.GetStatusAsync"/>.</summary>
/// <param name="State">The mapped payment state.</param>
/// <param name="TransactionId">Gateway transaction reference; set only when <paramref name="State"/> is <see cref="GatewayPaymentState.Paid"/>.</param>
/// <param name="RawStatus">The gateway's raw status value, for diagnostics.</param>
public sealed record GatewayPaymentStatus(
    GatewayPaymentState State,
    string? TransactionId,
    string? RawStatus);
