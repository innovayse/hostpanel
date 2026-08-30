namespace Innovayse.Domain.Tests.Billing;

using Innovayse.Domain.Billing;
using Xunit;

/// <summary>
/// Pins the behaviour of <see cref="InvoiceItem.ClientServiceId"/> — the link that lets a
/// customer ask what they were charged for one service.
/// </summary>
/// <remarks>
/// The link is nullable and there is no backfill, so the null carries meaning: a line the
/// platform never recorded a service for. These tests exist to stop that meaning being eroded —
/// by a default that quietly invents a link, by an edit path that silently drops one, or by a
/// duplicate that loses one.
/// </remarks>
public sealed class InvoiceItemServiceLinkTests
{
    /// <summary>A due date far enough out that nothing under test goes overdue.</summary>
    private static DateTimeOffset DueDate => DateTimeOffset.UtcNow.AddDays(14);

    /// <summary>
    /// A line raised without a service in hand carries no link. The eleven call sites that
    /// predate the column all take this path, and none of them may end up claiming a service.
    /// </summary>
    [Fact]
    public void AddItem_WithoutAServiceId_LeavesTheLinkUnset()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate);

        invoice.AddItem("Manual adjustment", 100m, 1);

        Assert.Null(Assert.Single(invoice.Items).ClientServiceId);
    }

    /// <summary>A line raised with a service in hand records it.</summary>
    [Fact]
    public void AddItem_WithAServiceId_RecordsIt()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate);

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
        var invoice = Invoice.Create(clientId: 1, DueDate);
        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        var item = Assert.Single(invoice.Items);

        invoice.UpdateItem(item.Id, "Renewal: Starter Hosting (corrected)", 5500m, 1);

        Assert.Equal(42, item.ClientServiceId);
        Assert.Equal(5500m, item.Amount);
    }

    /// <summary>
    /// A duplicate is the same charges billed again, so each line is still for the service it was
    /// for — and a line that was for none is still for none.
    /// </summary>
    [Fact]
    public void Duplicate_CarriesEachLinkAcrossUnchanged()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate);
        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        invoice.AddItem("Manual adjustment", 100m, 1);

        var copy = invoice.Duplicate();

        Assert.Equal(new int?[] { 42, null }, copy.Items.Select(i => i.ClientServiceId));
    }

    /// <summary>The link is carried by the line, not by the invoice: one invoice may mix them.</summary>
    [Fact]
    public void AnInvoiceMayCarryLinkedAndUnlinkedLinesTogether()
    {
        var invoice = Invoice.Create(clientId: 1, DueDate);

        invoice.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: 42);
        invoice.AddItem("Renewal: Backup Add-on", 500m, 1, clientServiceId: 43);
        invoice.AddItem("Late fee", 250m, 1);

        Assert.Equal(new int?[] { 42, 43, null }, invoice.Items.Select(i => i.ClientServiceId));
        Assert.Equal(5750m, invoice.SubTotal);
    }
}
