using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for <see cref="BillableItem"/>: being put on an invoice and being taken off it again.</summary>
public sealed class BillableItemTests
{
    /// <summary>Creates a one-time item that is already on an invoice.</summary>
    /// <param name="invoiceId">The invoice the item is put on.</param>
    /// <returns>The invoiced item.</returns>
    private static BillableItem InvoicedItem(int invoiceId)
    {
        var item = BillableItem.Create(1, "Consulting", 40m, "USD", BillableItemType.OneTime);
        item.MarkInvoiced(invoiceId);
        return item;
    }

    /// <summary>A currency given in lower case is stored upper-cased, as an invoice does.</summary>
    [Fact]
    public void Create_StoresTheCurrencyUpperCased()
    {
        var item = BillableItem.Create(1, "Consulting", 40m, "usd", BillableItemType.OneTime);

        item.Currency.Should().Be("USD");
    }

    /// <summary>A currency that is not three letters is refused.</summary>
    [Fact]
    public void Create_RefusesACurrencyThatIsNotThreeLetters()
    {
        var create = () => BillableItem.Create(1, "Consulting", 40m, "US", BillableItemType.OneTime);

        create.Should().Throw<ArgumentException>();
    }

    /// <summary>Releasing an invoiced item clears the flag and the invoice link, so it is billed again.</summary>
    [Fact]
    public void ReleaseFromInvoice_MakesTheItemWaitForInvoicingAgain()
    {
        var item = InvoicedItem(7);

        item.ReleaseFromInvoice();

        item.IsInvoiced.Should().BeFalse();
        item.InvoiceId.Should().BeNull();
    }

    /// <summary>Releasing an item that is on no invoice changes nothing.</summary>
    [Fact]
    public void ReleaseFromInvoice_LeavesAnUninvoicedItemAlone()
    {
        var item = BillableItem.Create(1, "Consulting", 40m, "USD", BillableItemType.OneTime);

        item.ReleaseFromInvoice();

        item.IsInvoiced.Should().BeFalse();
        item.InvoiceId.Should().BeNull();
    }

    /// <summary>Releasing keeps the item's due date: only the invoice link is undone.</summary>
    [Fact]
    public void ReleaseFromInvoice_KeepsTheDueDate()
    {
        var due = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var item = BillableItem.Create(1, "Hosting", 9m, "USD", BillableItemType.OneTime, nextDueDate: due);
        item.MarkInvoiced(3);

        item.ReleaseFromInvoice();

        item.NextDueDate.Should().Be(due);
    }
}
