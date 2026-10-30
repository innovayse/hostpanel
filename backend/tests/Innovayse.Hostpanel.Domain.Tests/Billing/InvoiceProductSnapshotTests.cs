using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// The product snapshot an <see cref="InvoiceItem"/> keeps, and the <see cref="Invoice.HasDigitalItems"/>
/// flag computed from it: carried by <see cref="Invoice.AddItem"/>, <see cref="Invoice.AddServiceRenewal"/>
/// and <see cref="Invoice.Duplicate"/>, and left alone by an edit of the line.
/// </summary>
public sealed class InvoiceProductSnapshotTests
{
    /// <summary>The fixed moment these tests treat as the present: issue times.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Fixed start of the renewal period the renewal tests use.</summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A digital product as it was sold: SKU and metadata included.</summary>
    private static readonly ProductSnapshot Digital =
        new(ProductId: 12, BillingCycle.Monthly, IsDigital: true, Sku: "CREDITS-100", Metadata: "{\"credits\":100}");

    /// <summary>A hosting product as it was sold: not digital, no SKU, no metadata.</summary>
    private static readonly ProductSnapshot Hosting =
        new(ProductId: 3, BillingCycle.Annual, IsDigital: false, Sku: null, Metadata: null);

    /// <summary>A line added with a snapshot keeps every value of it.</summary>
    [Fact]
    public void AddItem_WithASnapshot_KeepsIt()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);

        invoice.AddItem("Credits pack", 10m, 1, productSnapshot: Digital);

        var line = invoice.Items.Single();
        line.ProductSnapshot.Should().Be(Digital);
        line.ProductSnapshot!.ProductId.Should().Be(12);
        line.ProductSnapshot.BillingCycle.Should().Be(BillingCycle.Monthly);
        line.ProductSnapshot.Sku.Should().Be("CREDITS-100");
        line.ProductSnapshot.Metadata.Should().Be("{\"credits\":100}");
        line.IsDigital.Should().BeTrue();
    }

    /// <summary>A line added without a snapshot has none, and is not digital.</summary>
    [Fact]
    public void AddItem_WithoutASnapshot_HasNone()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);

        invoice.AddItem("Manual adjustment", 5m, 1);

        var line = invoice.Items.Single();
        line.ProductSnapshot.Should().BeNull();
        line.IsDigital.Should().BeFalse();
        invoice.HasDigitalItems.Should().BeFalse();
    }

    /// <summary>A service renewal line carries the snapshot it was given.</summary>
    [Fact]
    public void AddServiceRenewal_WithASnapshot_KeepsIt()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);

        invoice.AddServiceRenewal("Renewal: Credits", 10m, 42, Start, Start.AddMonths(1), Digital);

        var line = invoice.Items.Single();
        line.IsRenewal.Should().BeTrue();
        line.ProductSnapshot.Should().Be(Digital);
        invoice.HasDigitalItems.Should().BeTrue();
    }

    /// <summary>A domain renewal line never carries a product snapshot: a domain is not a catalogue product.</summary>
    [Fact]
    public void AddDomainRenewal_HasNoSnapshot()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);

        invoice.AddDomainRenewal("Renewal: a.com", 10m, 7, Start, Start.AddYears(1));

        invoice.Items.Single().ProductSnapshot.Should().BeNull();
        invoice.HasDigitalItems.Should().BeFalse();
    }

    /// <summary>A line for a product that is not digital leaves the invoice flag off.</summary>
    [Fact]
    public void HasDigitalItems_OnlyNonDigitalLines_IsFalse()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);

        invoice.AddItem("Hosting", 50m, 1, clientServiceId: 5, productSnapshot: Hosting);

        invoice.HasDigitalItems.Should().BeFalse();
    }

    /// <summary>The invoice flag follows the lines: on with a digital line, off again once that line is removed.</summary>
    [Fact]
    public void HasDigitalItems_FollowsTheLines()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);
        invoice.AddItem("Credits pack", 10m, 1, productSnapshot: Digital);
        invoice.HasDigitalItems.Should().BeTrue();

        invoice.RemoveItem(invoice.Items.Single().Id);

        invoice.HasDigitalItems.Should().BeFalse();
    }

    /// <summary>Editing a line's text, price or quantity leaves what was sold as it was.</summary>
    [Fact]
    public void UpdateItem_KeepsTheSnapshot()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);
        invoice.AddItem("Credits pack", 10m, 1, productSnapshot: Digital);
        var line = invoice.Items.Single();

        invoice.UpdateItem(line.Id, "Credits pack (corrected)", 12m, 2);

        line.ProductSnapshot.Should().Be(Digital);
        line.Amount.Should().Be(24m);
        invoice.HasDigitalItems.Should().BeTrue();
    }

    /// <summary>A duplicate sells what the original sold: each line keeps its snapshot, a line without one stays without.</summary>
    [Fact]
    public void Duplicate_CarriesEachSnapshotAcross()
    {
        var invoice = Invoice.Create(1, Now.AddDays(14), "USD", Now);
        invoice.AddItem("Credits pack", 10m, 1, productSnapshot: Digital);
        invoice.AddItem("Hosting", 50m, 1, clientServiceId: 5, productSnapshot: Hosting);
        invoice.AddItem("Manual adjustment", 5m, 1);

        var copy = invoice.Duplicate(Now);

        copy.Items.Select(i => i.ProductSnapshot).Should().Equal(Digital, Hosting, null);
        copy.HasDigitalItems.Should().BeTrue();
    }
}
