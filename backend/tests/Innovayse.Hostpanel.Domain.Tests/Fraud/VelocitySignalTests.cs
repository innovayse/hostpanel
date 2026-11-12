using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for <see cref="VelocitySignal"/>.</summary>
public sealed class VelocitySignalTests
{
    /// <summary>The signal fires once the orders already placed in the window reach the limit, so this one would be past it.</summary>
    /// <param name="orders">Orders placed from the address within the window.</param>
    /// <param name="fires">Whether the signal fires with a limit of three.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(8, true)]
    public void Evaluate_FiresAtTheLimit(int orders, bool fires)
    {
        var hit = new VelocitySignal().Evaluate(FraudContexts.Request(orders: orders, maxOrders: 3));

        (hit is not null).Should().Be(fires);
        hit?.Reason.Should().Be(FraudReasonCode.OrderVelocity);
    }

    /// <summary>Without an address there is nothing to count against.</summary>
    [Fact]
    public void Evaluate_NoAddress_DoesNotFire()
    {
        new VelocitySignal().Evaluate(FraudContexts.Request(ip: null, orders: 9)).Should().BeNull();
    }
}
