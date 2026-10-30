using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// <see cref="Invoice.IsPastDigitalGrace"/>: only digital renewal lines are judged, each against the grace
/// period its own snapshot recorded, counted from the due date, and the invoice is past grace only when all
/// of them are.
/// </summary>
public sealed class InvoiceDigitalGraceTests
{
    /// <summary>The due date of the invoices in these tests.</summary>
    private static readonly DateTimeOffset Due = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a snapshot for a product with the given digital flag and grace period.</summary>
    /// <param name="digital">Whether the product is digital.</param>
    /// <param name="graceDays">The grace period, or <see langword="null"/> for a snapshot that predates it.</param>
    /// <returns>The snapshot.</returns>
    private static ProductSnapshot Snapshot(bool digital, int? graceDays) =>
        new(4, BillingCycle.Monthly, digital, null, null, graceDays);

    /// <summary>Builds an invoice due at <see cref="Due"/>.</summary>
    /// <returns>The invoice, with no line yet.</returns>
    private static Invoice NewInvoice() => Invoice.Create(1, Due, "USD", Due.AddDays(-7));

    /// <summary>Adds a renewal line for a service over a month.</summary>
    /// <param name="invoice">The invoice to add to.</param>
    /// <param name="serviceId">The renewed service.</param>
    /// <param name="snapshot">The line's product snapshot.</param>
    private static void AddRenewal(Invoice invoice, int serviceId, ProductSnapshot? snapshot)
    {
        invoice.AddServiceRenewal("Renewal", 10m, serviceId, Due, Due.AddMonths(1), snapshot);
    }

    /// <summary>The grace boundary is strict: at the very end of the grace period the invoice is not past it, a moment later it is.</summary>
    /// <param name="secondsAfterGraceEnd">How far past the end of a 7 day grace period the check is made.</param>
    /// <param name="expected">Whether the invoice is past grace.</param>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void GraceBoundary_IsStrict(int secondsAfterGraceEnd, bool expected)
    {
        var invoice = NewInvoice();
        AddRenewal(invoice, 1, Snapshot(digital: true, graceDays: 7));

        invoice.IsPastDigitalGrace(Due.AddDays(7).AddSeconds(secondsAfterGraceEnd)).Should().Be(expected);
    }

    /// <summary>A product with no grace period lapses as soon as the due date has passed.</summary>
    [Fact]
    public void ZeroGrace_IsPastGraceRightAfterTheDueDate()
    {
        var invoice = NewInvoice();
        AddRenewal(invoice, 1, Snapshot(digital: true, graceDays: 0));

        invoice.IsPastDigitalGrace(Due).Should().BeFalse();
        invoice.IsPastDigitalGrace(Due.AddSeconds(1)).Should().BeTrue();
    }

    /// <summary>With two digital renewal lines the longer grace period decides: all of them must be past theirs.</summary>
    [Fact]
    public void TwoDigitalLines_AreAllPastGraceOnlyAfterTheLongerOne()
    {
        var invoice = NewInvoice();
        AddRenewal(invoice, 1, Snapshot(digital: true, graceDays: 3));
        AddRenewal(invoice, 2, Snapshot(digital: true, graceDays: 10));

        invoice.IsPastDigitalGrace(Due.AddDays(5)).Should().BeFalse();
        invoice.IsPastDigitalGrace(Due.AddDays(11)).Should().BeTrue();
    }

    /// <summary>A mixed invoice is judged on its digital renewal lines alone: the hosting line neither lapses it nor holds it back.</summary>
    [Fact]
    public void MixedInvoice_IsJudgedOnItsDigitalRenewalLinesOnly()
    {
        var invoice = NewInvoice();
        AddRenewal(invoice, 1, Snapshot(digital: false, graceDays: 30));
        AddRenewal(invoice, 2, Snapshot(digital: true, graceDays: 3));

        invoice.IsPastDigitalGrace(Due.AddDays(4)).Should().BeTrue();
    }

    /// <summary>An invoice with no digital renewal line is never past digital grace, whatever its other lines are.</summary>
    [Fact]
    public void WithoutDigitalRenewalLine_IsNeverPastGrace()
    {
        var hostingOnly = NewInvoice();
        AddRenewal(hostingOnly, 1, Snapshot(digital: false, graceDays: 0));

        var digitalOneOff = NewInvoice();
        digitalOneOff.AddItem("Licence", 10m, 1, productSnapshot: Snapshot(digital: true, graceDays: 0));

        var plain = NewInvoice();
        plain.AddItem("Setup", 10m, 1);

        var farLater = Due.AddDays(400);
        hostingOnly.IsPastDigitalGrace(farLater).Should().BeFalse();
        digitalOneOff.IsPastDigitalGrace(farLater).Should().BeFalse();
        plain.IsPastDigitalGrace(farLater).Should().BeFalse();
    }

    /// <summary>A digital renewal line whose snapshot predates the grace period is never judged past it, and holds the invoice back.</summary>
    [Fact]
    public void SnapshotWithoutGrace_IsNeverPastGrace()
    {
        var invoice = NewInvoice();
        AddRenewal(invoice, 1, Snapshot(digital: true, graceDays: null));

        invoice.IsPastDigitalGrace(Due.AddDays(400)).Should().BeFalse();

        AddRenewal(invoice, 2, Snapshot(digital: true, graceDays: 1));
        invoice.IsPastDigitalGrace(Due.AddDays(400)).Should().BeFalse();
    }
}
