using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>The <see cref="GatewayCustomer"/> record: what it keeps and what it refuses.</summary>
public sealed class GatewayCustomerTests
{
    /// <summary>The fixed moment these tests treat as the present.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creating keeps the client, the module and the gateway's reference.</summary>
    [Fact]
    public void Create_KeepsWhatTheGatewayReturned()
    {
        var customer = GatewayCustomer.Create(3, "bank-gateway", "cus-9", Now);

        customer.ClientId.Should().Be(3);
        customer.GatewayModuleId.Should().Be("bank-gateway");
        customer.CustomerRef.Should().Be("cus-9");
        customer.CreatedAt.Should().Be(Now);
    }

    /// <summary>An empty module or reference, or one wider than its column, is refused.</summary>
    [Fact]
    public void Create_WithAnInvalidModuleOrReference_Throws()
    {
        var noModule = () => GatewayCustomer.Create(3, " ", "cus-9", Now);
        var noReference = () => GatewayCustomer.Create(3, "bank-gateway", "", Now);
        var tooLong = () => GatewayCustomer.Create(3, "bank-gateway", new string('c', GatewayCustomer.MaxLength + 1), Now);

        noModule.Should().Throw<ArgumentException>();
        noReference.Should().Throw<ArgumentException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }
}
