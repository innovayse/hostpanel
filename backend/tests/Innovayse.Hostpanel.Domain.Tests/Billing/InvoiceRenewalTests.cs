using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Renewal lines on <see cref="Invoice"/>: construction, the applied marker, copies, full payment.</summary>
public sealed class InvoiceRenewalTests
{
    /// <summary>The fixed moment these tests treat as the present: issue, payment and overdue times.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Fixed start of the renewal period every test line uses.</summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A service renewal line carries its period and service, and is a renewal.</summary>
    [Fact]
    public void AddServiceRenewal_RecordsPeriodAndService()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);

        invoice.AddServiceRenewal("Renewal: Starter", 5m, 42, Start, Start.AddMonths(1));

        var line = invoice.Items.Single();
        line.IsRenewal.Should().BeTrue();
        line.ClientServiceId.Should().Be(42);
        line.RegisteredDomainId.Should().BeNull();
        line.RenewalPeriodStart.Should().Be(Start);
        line.RenewalPeriodEnd.Should().Be(Start.AddMonths(1));
        invoice.Total.Should().Be(5m);
    }

    /// <summary>A period that ends before it starts is a programming error.</summary>
    [Fact]
    public void AddDomainRenewal_EmptyPeriod_Throws()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);

        var act = () => invoice.AddDomainRenewal("Renewal: a.com", 10m, 7, Start, Start);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>The applied marker can be set once, and only on a renewal line.</summary>
    [Fact]
    public void MarkRenewalApplied_Once()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);
        invoice.AddServiceRenewal("r", 5m, 42, Start, Start.AddMonths(1));
        invoice.AddItem("plain", 1m, 1);
        var renewal = invoice.Items[0];

        renewal.MarkRenewalApplied(Start);

        renewal.RenewalAppliedAt.Should().Be(Start);
        ((Action)(() => renewal.MarkRenewalApplied(Start))).Should().Throw<InvalidOperationException>();
        ((Action)(() => invoice.Items[1].MarkRenewalApplied(Start))).Should().Throw<InvalidOperationException>();
    }

    /// <summary>A duplicated invoice keeps the service link but is not a renewal: paying it extends nothing.</summary>
    [Fact]
    public void Duplicate_DropsThePeriod()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);
        invoice.AddServiceRenewal("r", 5m, 42, Start, Start.AddMonths(1));

        var copy = invoice.Duplicate(Now);

        copy.Items.Single().ClientServiceId.Should().Be(42);
        copy.Items.Single().IsRenewal.Should().BeFalse();
    }

    /// <summary>A partial manual payment leaves the invoice open and not paid in full; a full one is paid in full.</summary>
    [Fact]
    public void IsPaidInFull_CountsRecordedPayments()
    {
        var partial = Invoice.Create(1, Start, "USD", Now);
        partial.AddItem("x", 10m, 1);
        partial.AddPayment(Start, "Bank", "tx1", 4m, 0m, null, TransactionSource.Manual);

        var full = Invoice.Create(1, Start, "USD", Now);
        full.AddItem("x", 10m, 1);
        full.AddPayment(Start, "Bank", "tx2", 10m, 0m, null, TransactionSource.Manual);

        var gateway = Invoice.Create(1, Start, "USD", Now);
        gateway.AddItem("x", 10m, 1);
        gateway.RecordPaidHistory("pi_1", Now);

        partial.IsPaidInFull.Should().BeFalse();
        partial.Status.Should().Be(InvoiceStatus.Unpaid);
        full.IsPaidInFull.Should().BeTrue();
        gateway.IsPaidInFull.Should().BeTrue();
    }

    /// <summary>A claim is taken once, refused while held, and can be released for a retry.</summary>
    [Fact]
    public void ClaimRenewal_IsExclusiveUntilReleased()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);
        invoice.AddServiceRenewal("r", 5m, 42, Start, Start.AddMonths(1));
        var line = invoice.Items.Single();

        line.ClaimRenewal(Start);

        line.RenewalAttemptAt.Should().Be(Start);
        FluentActions.Invoking(() => line.ClaimRenewal(Start)).Should().Throw<InvalidOperationException>();

        line.ReleaseRenewalClaim();

        line.RenewalAttemptAt.Should().BeNull();
        FluentActions.Invoking(() => line.ReleaseRenewalClaim()).Should().Throw<InvalidOperationException>();
    }

    /// <summary>A line that is not a renewal cannot be claimed.</summary>
    [Fact]
    public void ClaimRenewal_OnPlainLine_Throws()
    {
        var invoice = Invoice.Create(1, Start, "USD", Now);
        invoice.AddItem("x", 10m, 1);

        FluentActions.Invoking(() => invoice.Items.Single().ClaimRenewal(Start))
            .Should().Throw<InvalidOperationException>();
    }
}
