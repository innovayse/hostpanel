namespace Innovayse.Domain.Tests.Billing;

using FluentAssertions;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Events;

/// <summary>Unit tests for the <see cref="Invoice"/> aggregate.</summary>
public sealed class InvoiceTests
{
    /// <summary>Create sets all properties and raises InvoiceCreatedEvent.</summary>
    [Fact]
    public void Create_SetsPropertiesAndRaisesEvent()
    {
        var due = DateTimeOffset.UtcNow.AddDays(14);

        var invoice = Invoice.Create(clientId: 7, dueDate: due);

        invoice.ClientId.Should().Be(7);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DueDate.Should().Be(due);
        invoice.Total.Should().Be(0m);
        invoice.PaidAt.Should().BeNull();
        invoice.GatewayTransactionId.Should().BeNull();
        invoice.Items.Should().BeEmpty();
        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCreatedEvent>()
            .Which.ClientId.Should().Be(7);
    }

    /// <summary>AddItem increases Total and appends item to collection.</summary>
    [Fact]
    public void AddItem_IncreasesTotalAndAddsItem()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(7));

        invoice.AddItem("Hosting Plan", unitPrice: 10m, quantity: 2);

        invoice.Total.Should().Be(20m);
        invoice.Items.Should().HaveCount(1);
        invoice.Items[0].Description.Should().Be("Hosting Plan");
        invoice.Items[0].UnitPrice.Should().Be(10m);
        invoice.Items[0].Quantity.Should().Be(2);
        invoice.Items[0].Amount.Should().Be(20m);
    }

    /// <summary>AddItem twice accumulates Total correctly.</summary>
    [Fact]
    public void AddItem_Twice_AccumulatesTotal()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(7));

        invoice.AddItem("Domain", 12m, 1);
        invoice.AddItem("SSL", 5m, 3);

        invoice.Total.Should().Be(27m);
        invoice.Items.Should().HaveCount(2);
    }

    /// <summary>MarkPaid sets status, PaidAt, transactionId and raises PaymentReceivedEvent.</summary>
    [Fact]
    public void MarkPaid_SetsStatusAndRaisesEvent()
    {
        var invoice = Invoice.Create(clientId: 3, dueDate: DateTimeOffset.UtcNow.AddDays(7));
        invoice.AddItem("VPS", 20m, 1);
        invoice.ClearDomainEvents();

        invoice.MarkPaid("txn_abc123");

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().NotBeNull();
        invoice.GatewayTransactionId.Should().Be("txn_abc123");
        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentReceivedEvent>()
            .Which.TransactionId.Should().Be("txn_abc123");
    }

    /// <summary>MarkPaid on a non-payable invoice throws InvalidOperationException.</summary>
    [Fact]
    public void MarkPaid_WhenAlreadyPaid_Throws()
    {
        var invoice = Invoice.Create(clientId: 3, dueDate: DateTimeOffset.UtcNow.AddDays(7));
        invoice.MarkPaid("txn_1");

        var act = () => invoice.MarkPaid("txn_2");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>MarkOverdue transitions from Unpaid to Overdue and raises InvoiceOverdueEvent.</summary>
    [Fact]
    public void MarkOverdue_SetsOverdueAndRaisesEvent()
    {
        var invoice = Invoice.Create(clientId: 5, dueDate: DateTimeOffset.UtcNow.AddDays(-1));
        invoice.ClearDomainEvents();

        invoice.MarkOverdue();

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceOverdueEvent>();
    }

    /// <summary>Cancel changes status to Cancelled for an Unpaid invoice.</summary>
    [Fact]
    public void Cancel_WhenUnpaid_SetsCancelled()
    {
        var invoice = Invoice.Create(clientId: 2, dueDate: DateTimeOffset.UtcNow.AddDays(7));

        invoice.Cancel();

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>Cancel on a Paid invoice throws InvalidOperationException.</summary>
    [Fact]
    public void Cancel_WhenPaid_Throws()
    {
        var invoice = Invoice.Create(clientId: 2, dueDate: DateTimeOffset.UtcNow.AddDays(7));
        invoice.MarkPaid("txn_99");

        var act = () => invoice.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>MarkOverdue called twice is idempotent — no duplicate event raised.</summary>
    [Fact]
    public void MarkOverdue_WhenAlreadyOverdue_IsIdempotent()
    {
        var invoice = Invoice.Create(clientId: 5, dueDate: DateTimeOffset.UtcNow.AddDays(-1));
        invoice.MarkOverdue();
        invoice.ClearDomainEvents();

        var act = () => invoice.MarkOverdue();

        act.Should().NotThrow();
        invoice.DomainEvents.Should().BeEmpty();
    }

    /// <summary>AddItem on an Overdue invoice throws InvalidOperationException.</summary>
    [Fact]
    public void AddItem_WhenOverdue_Throws()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(-1));
        invoice.MarkOverdue();

        var act = () => invoice.AddItem("SSL", 5m, 1);

        act.Should().Throw<InvalidOperationException>();
    }
}
