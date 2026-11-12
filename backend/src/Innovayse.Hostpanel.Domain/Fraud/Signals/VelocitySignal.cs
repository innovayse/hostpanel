using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when this order would take the orders from one IP address within the velocity window past the limit.</summary>
public sealed class VelocitySignal : IFraudSignal
{
    /// <summary>Points a burst of orders adds.</summary>
    public const int Weight = 50;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The orders already placed plus this one would exceed the limit exactly when the placed ones reach it.
        return string.IsNullOrWhiteSpace(context.IpAddress) || context.OrdersInWindow < context.VelocityMaxOrders
            ? null
            : new FraudSignalHit(FraudReasonCode.OrderVelocity, Weight);
    }
}
