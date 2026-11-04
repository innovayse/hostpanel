using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>The <see cref="GatewayCustomer"/> link between a client and its account at one payment gateway module.</summary>
public sealed class GatewayCustomerTests
{
    /// <summary>The link carries the client, the module, the gateway's reference and the moment it was made.</summary>
    [Fact]
    public void Create_SetsClientModuleReferenceAndTime()
    {
        var now = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

        var customer = GatewayCustomer.Create(42, "stripe", "cus_123", now);

        customer.ClientId.Should().Be(42);
        customer.GatewayModuleId.Should().Be("stripe");
        customer.CustomerRef.Should().Be("cus_123");
        customer.CreatedAt.Should().Be(now);
    }
}
