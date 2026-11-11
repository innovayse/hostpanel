using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// Pins the behaviour of <see cref="InvoiceItem.ClientServiceId"/>, the link that lets a
/// customer ask what they were charged for one service.
/// </summary>
/// <remarks>
/// The link is nullable and there is no backfill, so the null carries meaning: a line the
/// platform never recorded a service for. These tests exist to stop that meaning being eroded
/// by a default that quietly invents a link, by an edit path that silently drops one, or by a
/// duplicate that loses one.
/// </remarks>
public sealed class InvoiceItemTests
{
    /// <summary>The fixed moment these tests treat as the present: issue, payment and overdue times.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A due date far enough out that nothing under test goes overdue.</summary>
    private static DateTimeOffset DueDate => Now.AddDays(14);

    /// <summary>
    /// A line raised without a service in hand carries no link. The eleven call sites that
    /// predate the column all take this path, and none of them may end up claiming a service.
    /// </summary>
    [Fact]
    public void AddItem_WithoutAServiceId_LeavesTheLinkUnset()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        invoice.AddItem("Manual adjustment", 100m, 1);

        Assert.Null(Assert.Single(invoice.Items).ClientServiceId);
    }

    /// <summary>A line raised with a service in hand records it.</summary>
    [Fact]
    public void AddItem_WithAServiceId_RecordsIt()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);

        Assert.Equal(42, Assert.Single(invoice.Items).ClientServiceId);
    }

    /// <summary>
    /// Re-wording a description or correcting a price does not change which service the money
    /// was for. An edit that cleared the link would make a real charge vanish from the service
    /// it belongs to.
    /// </summary>
    [Fact]
    public void UpdateItem_KeepsTheServiceLink()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);
        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        var item = Assert.Single(invoice.Items);

        invoice.UpdateItem(item.Id, "Renewal: Starter Hosting (corrected)", 5500m, 1);

        Assert.Equal(42, item.ClientServiceId);
        Assert.Equal(5500m, item.Amount);
    }

    /// <summary>
    /// A duplicate is the same charges billed again, so each line is still for the service it was
    /// for, and a line that was for none is still for none.
    /// </summary>
    [Fact]
    public void Duplicate_CarriesEachLinkAcrossUnchanged()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);
        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        invoice.AddItem("Manual adjustment", 100m, 1);

        var copy = invoice.Duplicate(Now);

        Assert.Equal(new int?[] { 42, null }, copy.Items.Select(i => i.ClientServiceId));
    }

    /// <summary>The link is carried by the line, not by the invoice: one invoice may mix them.</summary>
    [Fact]
    public void AnInvoiceMayCarryLinkedAndUnlinkedLinesTogether()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        invoice.AddItem("Renewal: Backup Add-on", 500m, 1, clientServiceId: 43);
        invoice.AddItem("Late fee", 250m, 1);

        Assert.Equal(new int?[] { 42, 43, null }, invoice.Items.Select(i => i.ClientServiceId));
        Assert.Equal(5750m, invoice.SubTotal);
    }

    /// <summary>A line billed without a discount carries 0 as the discount it was given.</summary>
    [Fact]
    public void AddItem_WithoutADiscount_RecordsNone()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        invoice.AddItem("Starter Hosting", 40m, 1);

        Assert.Equal(0m, invoice.Items.Single().DiscountPercent);
    }

    /// <summary>The line keeps the discount it was billed with, beside the discounted price it was billed at.</summary>
    [Fact]
    public void AddItem_WithADiscount_KeepsItAsTheSnapshot()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        invoice.AddItem("Starter Hosting", 30m, 1, discountPercent: 25m);

        var item = invoice.Items.Single();
        Assert.Equal(25m, item.DiscountPercent);
        Assert.Equal(30m, item.Amount);
        Assert.Equal(30m, invoice.SubTotal);
    }

    /// <summary>A discount outside 0 to 100 percent cannot be recorded on a line.</summary>
    /// <param name="discountPercent">A discount outside the range.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void AddItem_WithADiscountOutsideTheRange_IsRefused(double discountPercent)
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.AddItem("Starter Hosting", 30m, 1, discountPercent: (decimal)discountPercent));
    }

    /// <summary>Editing a line's price or wording leaves the discount it was billed with as it was.</summary>
    [Fact]
    public void UpdateItem_LeavesTheDiscountAlone()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);
        invoice.AddItem("Starter Hosting", 30m, 1, discountPercent: 25m);

        invoice.UpdateItem(invoice.Items.Single().Id, "Starter Hosting, annual", 50m, 1);

        Assert.Equal(25m, invoice.Items.Single().DiscountPercent);
    }

    /// <summary>A duplicate carries each line's discount across and discounts nothing twice.</summary>
    [Fact]
    public void Duplicate_CarriesTheDiscountAcrossWithoutDiscountingAgain()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate, "USD", Now);
        invoice.AddItem("Starter Hosting", 30m, 1, discountPercent: 25m);

        var copy = invoice.Duplicate(Now);

        var item = copy.Items.Single();
        Assert.Equal(25m, item.DiscountPercent);
        Assert.Equal(30m, item.UnitPrice);
    }

    /// <summary>The discount comes off the list price and the result is rounded to the currency's minor unit.</summary>
    /// <param name="listPrice">The price before the discount.</param>
    /// <param name="discountPercent">The discount in percent.</param>
    /// <param name="currency">The currency of the price.</param>
    /// <param name="expected">The price after the discount.</param>
    [Theory]
    [InlineData(40, 25, "USD", 30)]
    [InlineData(19.99, 15, "USD", 16.99)]
    [InlineData(100, 0, "USD", 100)]
    [InlineData(100, 100, "USD", 0)]
    [InlineData(999, 10, "JPY", 899)]
    public void PriceAfterDiscount_TakesThePercentOffAndRoundsToTheMinorUnit(
        double listPrice, double discountPercent, string currency, double expected)
    {
        var price = InvoiceItem.PriceAfterDiscount((decimal)listPrice, (decimal)discountPercent, currency);

        price.Should().Be((decimal)expected);
    }

    /// <summary>A discount outside 0 to 100 percent is refused, so a price can never grow or turn negative.</summary>
    /// <param name="discountPercent">A discount outside the range.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void PriceAfterDiscount_RefusesADiscountOutsideTheRange(double discountPercent)
    {
        var act = () => InvoiceItem.PriceAfterDiscount(10m, (decimal)discountPercent, "USD");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
