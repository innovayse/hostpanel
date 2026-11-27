using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Orders;

/// <summary>Unit tests for <see cref="OrderItem"/> domain fields.</summary>
public sealed class OrderItemTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>AddItem with domain action stores all domain fields on the order item.</summary>
    [Fact]
    public void AddItem_WithDomainAction_StoresDomainFields()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        order.AddItem(
            productId: 99,
            productName: "Domain Registration",
            billingCycle: BillingCycle.Annual,
            firstPaymentAmount: 12.99m,
            recurringAmount: 12.99m,
            domain: "example.com",
            hostname: null,
            domainAction: DomainAction.Register,
            eppCode: null,
            years: 1);

        var item = order.Items[0];
        Assert.Equal(DomainAction.Register, item.DomainAction);
        Assert.Null(item.EppCode);
        Assert.Equal(1, item.Years);
    }

    /// <summary>A domain line keeps the registry's extra values it was ordered with, as one stored JSON text.</summary>
    [Fact]
    public void AddItem_WithExtendedAttributes_KeepsThemOnTheLine()
    {
        var order = Order.Create("ORD-0003", 1, PaymentMethodNames.Card, null, "USD", Now);
        var values = DomainExtendedAttributes.From(new Dictionary<string, string> { ["holder-number"] = "N-1" });

        order.AddItem(
            99, "Domain Registration", BillingCycle.Annual, 12.99m, 12.99m, "name.alpha", null,
            DomainAction.Register, years: 1, extendedAttributes: values);

        var item = order.Items[0];
        item.ExtendedAttributes.Should().Be(values);
        item.ExtendedAttributesJson.Should().Be(values.Serialize());
    }

    /// <summary>A line ordered without extra values stores none.</summary>
    [Fact]
    public void AddItem_WithoutExtendedAttributes_StoresNothing()
    {
        var order = Order.Create("ORD-0004", 1, PaymentMethodNames.Card, null, "USD", Now);

        order.AddItem(99, "Hosting", BillingCycle.Monthly, 5m, 5m, null, null);

        order.Items[0].ExtendedAttributes.IsEmpty.Should().BeTrue();
        order.Items[0].ExtendedAttributesJson.Should().BeNull();
    }

    /// <summary>AddItem with transfer action stores EPP code.</summary>
    [Fact]
    public void AddItem_WithTransferAction_StoresEppCode()
    {
        var order = Order.Create("ORD-0002", 1, PaymentMethodNames.Card, null, "USD", Now);

        order.AddItem(
            productId: 99,
            productName: "Domain Registration",
            billingCycle: BillingCycle.Annual,
            firstPaymentAmount: 12.99m,
            recurringAmount: 12.99m,
            domain: "transfer.com",
            hostname: null,
            domainAction: DomainAction.Transfer,
            eppCode: "EPP-ABC-123",
            years: 1);

        var item = order.Items[0];
        Assert.Equal(DomainAction.Transfer, item.DomainAction);
        Assert.Equal("EPP-ABC-123", item.EppCode);
    }

    /// <summary>AddItem without domain action leaves domain fields null (hosting item).</summary>
    [Fact]
    public void AddItem_WithoutDomainAction_LeavesFieldsNull()
    {
        var order = Order.Create("ORD-0003", 1, PaymentMethodNames.Card, null, "USD", Now);

        order.AddItem(
            productId: 5,
            productName: "Starter Hosting",
            billingCycle: BillingCycle.Monthly,
            firstPaymentAmount: 4.99m,
            recurringAmount: 4.99m,
            domain: null,
            hostname: null,
            domainAction: null,
            eppCode: null,
            years: null);

        var item = order.Items[0];
        Assert.Null(item.DomainAction);
        Assert.Null(item.EppCode);
        Assert.Null(item.Years);
    }
}
