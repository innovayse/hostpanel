using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>
/// <see cref="Invoice.MarkLapsed"/>: only an overdue invoice lapses, it lapses once, and the lapse raises
/// <see cref="SubscriptionLapsedEvent"/> with the moment it was handed.
/// </summary>
public sealed class InvoiceLapseTests
{
    /// <summary>The fixed moment the invoices in these tests are issued at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The fixed moment, after the grace period, the invoices are marked lapsed at.</summary>
    private static readonly DateTimeOffset LapseMoment = Now.AddDays(30);

    /// <summary>Builds an overdue invoice with one line, digital or not.</summary>
    /// <param name="digital">Whether the line charges for a digital product.</param>
    /// <returns>The overdue invoice, its events cleared so only what the test does is seen.</returns>
    private static Invoice Overdue(bool digital)
    {
        var invoice = Invoice.Create(1, Now.AddDays(7), "USD", Now);
        invoice.AddItem("Plan", 10m, 1, productSnapshot: new ProductSnapshot(4, BillingCycle.Monthly, digital, null, null));
        invoice.MarkOverdue(Now.AddDays(8));
        invoice.ClearDomainEvents();
        return invoice;
    }

    /// <summary>An overdue invoice lapses: the moment is kept, the status stays Overdue and the event is raised once.</summary>
    [Fact]
    public void MarkLapsed_Overdue_SetsLapsedAtAndRaisesTheEvent()
    {
        var invoice = Overdue(digital: true);
        invoice.CanLapse.Should().BeTrue();

        invoice.MarkLapsed(LapseMoment);

        invoice.LapsedAt.Should().Be(LapseMoment);
        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.CanLapse.Should().BeFalse();
        var lapsed = invoice.DomainEvents.OfType<SubscriptionLapsedEvent>().Single();
        lapsed.ClientId.Should().Be(1);
        lapsed.HasDigitalItems.Should().BeTrue();
        lapsed.OccurredAt.Should().Be(LapseMoment);
    }

    /// <summary>The event says whether the invoice charges for anything digital.</summary>
    [Fact]
    public void MarkLapsed_WithoutDigitalLines_ReportsNone()
    {
        var invoice = Overdue(digital: false);

        invoice.MarkLapsed(LapseMoment);

        invoice.DomainEvents.OfType<SubscriptionLapsedEvent>().Single().HasDigitalItems.Should().BeFalse();
    }

    /// <summary>Marking a lapsed invoice again changes nothing and raises no second event.</summary>
    [Fact]
    public void MarkLapsed_Twice_IsANoOp()
    {
        var invoice = Overdue(digital: true);
        invoice.MarkLapsed(LapseMoment);

        invoice.MarkLapsed(LapseMoment.AddDays(1));

        invoice.LapsedAt.Should().Be(LapseMoment);
        invoice.DomainEvents.OfType<SubscriptionLapsedEvent>().Should().ContainSingle();
    }

    /// <summary>An invoice paid after it lapsed keeps the fact, and asking again is still a no-op rather than a refusal.</summary>
    [Fact]
    public void MarkLapsed_PaidAfterLapsing_KeepsTheFact()
    {
        var invoice = Overdue(digital: true);
        invoice.MarkLapsed(LapseMoment);
        invoice.Settle("bank", "ref-1", LapseMoment.AddDays(1));

        var act = () => invoice.MarkLapsed(LapseMoment.AddDays(2));

        act.Should().NotThrow();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.LapsedAt.Should().Be(LapseMoment);
        invoice.CanLapse.Should().BeFalse();
    }

    /// <summary>An invoice that is not overdue does not lapse: unpaid and paid ones are refused with the lapse code.</summary>
    [Fact]
    public void MarkLapsed_NotOverdue_IsRefused()
    {
        var unpaid = Invoice.Create(1, Now.AddDays(7), "USD", Now);
        unpaid.AddItem("Plan", 10m, 1);
        var paid = Invoice.Create(1, Now.AddDays(7), "USD", Now);
        paid.AddItem("Plan", 10m, 1);
        paid.Settle("bank", "ref-1", Now);

        foreach (var invoice in new[] { unpaid, paid })
        {
            invoice.CanLapse.Should().BeFalse();
            var act = () => invoice.MarkLapsed(LapseMoment);
            act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotOverdue);
            invoice.LapsedAt.Should().BeNull();
        }
    }
}
