using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Billing;

/// <summary>Unit tests for the <see cref="Invoice"/> aggregate.</summary>
public sealed class InvoiceTests
{
    /// <summary>The fixed moment these tests treat as the present: issue, payment and overdue times.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The moment every gateway attempt in these tests is registered.</summary>
    private static readonly DateTimeOffset GatewayAttemptAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Create sets all properties and raises InvoiceCreatedEvent.</summary>
    [Fact]
    public void Create_SetsPropertiesAndRaisesEvent()
    {
        var due = Now.AddDays(14);

        var invoice = Invoice.Create(clientId: 7, dueDate: due, currency: "USD", issuedAt: Now);

        invoice.ClientId.Should().Be(7);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DueDate.Should().Be(due);
        invoice.Total.Should().Be(0m);
        invoice.PaidAt.Should().BeNull();
        invoice.GatewayTransactionId.Should().BeNull();
        invoice.Items.Should().BeEmpty();
        invoice.InvoiceDate.Should().Be(Now);
        var created = invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCreatedEvent>().Subject;
        created.ClientId.Should().Be(7);
        created.Currency.Should().Be("USD");
        created.Total.Should().Be(0m);
        created.OccurredAt.Should().Be(Now);
    }

    /// <summary>The creation event carries the total the invoice has when it is dispatched, items included.</summary>
    [Fact]
    public void Create_EventCarriesTheTotalAfterItemsAreAdded()
    {
        var invoice = Invoice.Create(clientId: 7, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting", unitPrice: 12.5m, quantity: 1);

        var created = invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceCreatedEvent>().Subject;

        created.Total.Should().Be(invoice.Total);
        created.Total.Should().BeGreaterThan(0m);
    }

    /// <summary>AddItem increases Total and appends item to collection.</summary>
    [Fact]
    public void AddItem_IncreasesTotalAndAddsItem()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

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
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

        invoice.AddItem("Domain", 12m, 1);
        invoice.AddItem("SSL", 5m, 3);

        invoice.Total.Should().Be(27m);
        invoice.Items.Should().HaveCount(2);
    }

    /// <summary>MarkPaid on an invoice that owes nothing sets status, PaidAt, transactionId and raises PaymentReceivedEvent.</summary>
    [Fact]
    public void MarkPaid_SetsStatusAndRaisesEvent()
    {
        var invoice = Invoice.Create(clientId: 3, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("VPS", 20m, 1);
        invoice.ApplyCredit(20m);
        invoice.ClearDomainEvents();

        var paidAt = Now.AddHours(3);

        invoice.MarkPaid("txn_abc123", paidAt);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(paidAt);
        invoice.GatewayTransactionId.Should().Be("txn_abc123");
        var received = invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentReceivedEvent>().Subject;
        received.TransactionId.Should().Be("txn_abc123");
        received.OccurredAt.Should().Be(paidAt);
    }

    /// <summary>MarkPaid on a non-payable invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_NOT_PAYABLE</c>.</summary>
    [Fact]
    public void MarkPaid_WhenAlreadyPaid_ThrowsWithInvoiceNotPayable()
    {
        var invoice = Invoice.Create(clientId: 3, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.MarkPaid("txn_1", Now);

        var act = () => invoice.MarkPaid("txn_2", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPayable);
    }

    /// <summary>MarkOverdue transitions from Unpaid to Overdue and raises InvoiceOverdueEvent.</summary>
    [Fact]
    public void MarkOverdue_SetsOverdueAndRaisesEvent()
    {
        var invoice = Invoice.Create(clientId: 5, dueDate: Now.AddDays(-1), currency: "USD", issuedAt: Now);
        invoice.ClearDomainEvents();

        var overdueAt = Now.AddDays(1);

        invoice.MarkOverdue(overdueAt);

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InvoiceOverdueEvent>()
            .Which.OccurredAt.Should().Be(overdueAt);
    }

    /// <summary>Cancel changes status to Cancelled for an Unpaid invoice.</summary>
    [Fact]
    public void Cancel_WhenUnpaid_SetsCancelled()
    {
        var invoice = Invoice.Create(clientId: 2, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

        invoice.Cancel();

        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>Cancel on a Paid invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_CANCEL_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Cancel_WhenPaid_ThrowsWithInvoiceCancelNotAllowed()
    {
        var invoice = Invoice.Create(clientId: 2, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.MarkPaid("txn_99", Now);

        var act = () => invoice.Cancel();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceCancelNotAllowed);
    }

    /// <summary>MarkOverdue called twice is idempotent: no duplicate event is raised.</summary>
    [Fact]
    public void MarkOverdue_WhenAlreadyOverdue_IsIdempotent()
    {
        var invoice = Invoice.Create(clientId: 5, dueDate: Now.AddDays(-1), currency: "USD", issuedAt: Now);
        invoice.MarkOverdue(Now);
        invoice.ClearDomainEvents();

        var act = () => invoice.MarkOverdue(Now);

        act.Should().NotThrow();
        invoice.DomainEvents.Should().BeEmpty();
    }

    /// <summary>AddItem on an Overdue invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_NOT_EDITABLE</c>.</summary>
    [Fact]
    public void AddItem_WhenOverdue_ThrowsWithInvoiceNotEditable()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(-1), currency: "USD", issuedAt: Now);
        invoice.MarkOverdue(Now);

        var act = () => invoice.AddItem("SSL", 5m, 1);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
    }

    /// <summary>Publish on a non-Draft invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_NOT_DRAFT</c>.</summary>
    [Fact]
    public void Publish_WhenNotDraft_ThrowsWithInvoiceNotDraft()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

        var act = invoice.Publish;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotDraft);
    }

    /// <summary>
    /// RemoveItem with an id that names no line on the invoice throws <see cref="DomainRuleException"/>
    /// with <c>INVOICE_ITEM_NOT_FOUND</c>.
    /// </summary>
    [Fact]
    public void RemoveItem_WhenIdUnknown_ThrowsWithInvoiceItemNotFound()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

        var act = () => invoice.RemoveItem(999);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceItemNotFound);
    }

    /// <summary>
    /// MarkOverdue on a Draft invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_NOT_UNPAID</c>.
    /// </summary>
    [Fact]
    public void MarkOverdue_WhenDraft_ThrowsWithInvoiceNotUnpaid()
    {
        var invoice = Invoice.CreateDraft(clientId: 1, dueDate: Now.AddDays(-1), currency: "USD", issuedAt: Now);

        var act = () => invoice.MarkOverdue(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotUnpaid);
    }

    /// <summary>MarkUnpaid on an Unpaid invoice throws <see cref="DomainRuleException"/> with <c>INVOICE_NOT_PAID</c>.</summary>
    [Fact]
    public void MarkUnpaid_WhenNotPaid_ThrowsWithInvoiceNotPaid()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);

        var act = invoice.MarkUnpaid;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPaid);
    }

    /// <summary>CreateForBillableItems builds an unpaid invoice with one single-quantity line per item.</summary>
    [Fact]
    public void CreateForBillableItems_AddsOneLinePerItemAtItsAmount()
    {
        var due = Now.AddDays(14);
        var items = new[]
        {
            BillableItem.Create(7, "Setup fee", 25m, "USD", BillableItemType.OneTime),
            BillableItem.Create(7, "Extra IP", 4.5m, "USD", BillableItemType.OneTime),
        };

        var invoice = Invoice.CreateForBillableItems(clientId: 7, dueDate: due, currency: "USD", items, issuedAt: Now);

        invoice.ClientId.Should().Be(7);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DueDate.Should().Be(due);
        invoice.Items.Select(i => (i.Description, i.Quantity, i.Amount))
            .Should().Equal(("Setup fee", 1, 25m), ("Extra IP", 1, 4.5m));
        invoice.Total.Should().Be(29.5m);
        invoice.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InvoiceCreatedEvent>();
    }

    /// <summary>CreateForBillableItems with no items yields an empty unpaid invoice.</summary>
    [Fact]
    public void CreateForBillableItems_WithNoItems_YieldsEmptyInvoice()
    {
        var invoice = Invoice.CreateForBillableItems(
            clientId: 1, dueDate: Now.AddDays(7), currency: "USD", [], issuedAt: Now);

        invoice.Items.Should().BeEmpty();
        invoice.Total.Should().Be(0m);
    }

    /// <summary>The currency given at creation is what the invoice reports, upper-cased.</summary>
    [Fact]
    public void Create_StampsTheCurrency()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "usd", issuedAt: Now);
        Assert.Equal("USD", invoice.Currency);
    }

    /// <summary>A draft carries its currency too; it is not decided later at "send".</summary>
    [Fact]
    public void CreateDraft_StampsTheCurrency()
    {
        var invoice = Invoice.CreateDraft(clientId: 1, dueDate: Now.AddDays(7), currency: "AMD", issuedAt: Now);
        Assert.Equal("AMD", invoice.Currency);
    }

    /// <summary>The four-argument overload stamps the currency on both a draft and an unpaid invoice.</summary>
    /// <param name="isDraft">Whether the invoice is created as a draft.</param>
    /// <param name="expected">The status the invoice should start in.</param>
    [Theory]
    [InlineData(true, InvoiceStatus.Draft)]
    [InlineData(false, InvoiceStatus.Unpaid)]
    public void Create_WithDraftFlag_StampsTheCurrencyAndStatus(bool isDraft, InvoiceStatus expected)
    {
        var invoice = Invoice.Create(1, Now.AddDays(7), "eur", isDraft, Now);
        Assert.Equal("EUR", invoice.Currency);
        Assert.Equal(expected, invoice.Status);
    }

    /// <summary>A duplicate bills the same lines in the same currency as the original.</summary>
    [Fact]
    public void Duplicate_KeepsTheCurrency()
    {
        var original = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "AMD", issuedAt: Now);
        original.AddItem("Hosting", 1000m, 1);

        var copiedAt = Now.AddDays(2);

        var copy = original.Duplicate(copiedAt);

        Assert.Equal("AMD", copy.Currency);
        Assert.Equal(InvoiceStatus.Draft, copy.Status);
        Assert.Equal(copiedAt, copy.InvoiceDate);
    }

    /// <summary>Creating an invoice with a missing or malformed currency code throws.</summary>
    /// <param name="currency">A missing or malformed currency code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Create_RejectsAMissingOrMalformedCurrency(string currency)
        => Assert.Throws<ArgumentException>(() => Invoice.Create(1, Now.AddDays(7), currency, Now));

    /// <summary>Set gateway session, on unpaid invoice, stores module order id and timestamp.</summary>
    [Fact]
    public void SetGatewaySession_OnUnpaidInvoice_StoresModuleOrderIdAndTimestamp()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);

        invoice.SetGatewaySession("bank-gateway", "32faa424-858a-4f22", GatewayAttemptAt);

        Assert.Equal("bank-gateway", invoice.GatewayModule);
        Assert.Equal("32faa424-858a-4f22", invoice.GatewayOrderId);
        Assert.Equal(GatewayAttemptAt, invoice.GatewayStartedAt);
    }

    /// <summary>Set gateway session, second attempt, overwrites previous session.</summary>
    [Fact]
    public void SetGatewaySession_SecondAttempt_OverwritesPreviousSession()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "first-attempt", GatewayAttemptAt);

        invoice.SetGatewaySession("bank-gateway", "second-attempt", GatewayAttemptAt);

        Assert.Equal("second-attempt", invoice.GatewayOrderId);
    }

    /// <summary>Each recorded check stamps its moment and counts one more ask; a final answer concludes the session.</summary>
    [Fact]
    public void RecordGatewayCheck_CountsAsksAndConcludesOnAFinalAnswer()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "attempt", GatewayAttemptAt);

        invoice.RecordGatewayCheck(GatewayAttemptAt.AddMinutes(30), concluded: false);
        invoice.RecordGatewayCheck(GatewayAttemptAt.AddMinutes(40), concluded: false);

        Assert.Equal(2, invoice.GatewayCheckCount);
        Assert.Equal(GatewayAttemptAt.AddMinutes(40), invoice.GatewayCheckedAt);
        Assert.Null(invoice.GatewayConcludedAt);

        invoice.RecordGatewayCheck(GatewayAttemptAt.AddMinutes(60), concluded: true);

        Assert.Equal(GatewayAttemptAt.AddMinutes(60), invoice.GatewayConcludedAt);
    }

    /// <summary>A new session starts with a clean back-off: the bookkeeping of the previous session is forgotten.</summary>
    [Fact]
    public void SetGatewaySession_ForgetsTheChecksOfThePreviousSession()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "first-attempt", GatewayAttemptAt);
        invoice.RecordGatewayCheck(GatewayAttemptAt.AddMinutes(30), concluded: true);
        invoice.MarkGatewayStalledNotified(GatewayAttemptAt.AddHours(25));

        invoice.SetGatewaySession("bank-gateway", "second-attempt", GatewayAttemptAt.AddDays(2));

        Assert.Equal(0, invoice.GatewayCheckCount);
        Assert.Null(invoice.GatewayCheckedAt);
        Assert.Null(invoice.GatewayConcludedAt);
        Assert.Null(invoice.GatewayStalledNotifiedAt);
    }

    /// <summary>Set gateway session, on paid invoice, throws.</summary>
    [Fact]
    public void SetGatewaySession_OnPaidInvoice_Throws()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "attempt-1", GatewayAttemptAt);
        invoice.MarkPaid("txn-1", Now);

        Assert.Throws<InvalidOperationException>(
            () => invoice.SetGatewaySession("bank-gateway", "attempt-2", GatewayAttemptAt));
    }

    /// <summary>
    /// Mark paid while a gateway session is open keeps the session: the payer may still complete it,
    /// and that late payment must be found. The session took nothing, so the invoice was not paid through it.
    /// </summary>
    [Fact]
    public void MarkPaid_WithOpenGatewaySession_KeepsItButNotAsThePayer()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "open-session", GatewayAttemptAt);

        invoice.MarkPaid("card-txn-1", Now);

        Assert.Equal("bank-gateway", invoice.GatewayModule);
        Assert.Equal("open-session", invoice.GatewayOrderId);
        Assert.NotNull(invoice.GatewayStartedAt);
        Assert.False(invoice.WasPaidThrough("bank-gateway"));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    /// <summary>Settling through the active gateway session retains that session.</summary>
    [Fact]
    public void Settle_ThroughActiveGatewaySession_RetainsTheSession()
    {
        // The completion path for the invoice's own live gateway session: the session fields
        // must be retained so refund/reconciliation code can identify which gateway paid.
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);

        invoice.Settle("bank-gateway", "gw-order-1", Now);

        Assert.Equal("bank-gateway", invoice.GatewayModule);
        Assert.Equal("gw-order-1", invoice.GatewayOrderId);
        Assert.NotNull(invoice.GatewayStartedAt);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    /// <summary>Settle on an already-paid invoice refuses with <c>INVOICE_NOT_PAYABLE</c>.</summary>
    [Fact]
    public void Settle_OnPaidInvoice_ThrowsWithInvoiceNotPayable()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);
        invoice.Settle("bank-gateway", "gw-order-1", Now);

        var ex = Assert.Throws<DomainRuleException>(
            () => invoice.Settle("bank-gateway", "gw-order-2", Now));
        Assert.Equal(BillingErrors.InvoiceNotPayable, ex.Code);
    }

    /// <summary>Mark unpaid, on gateway paid invoice, clears session so reconciler cannot find it.</summary>
    [Fact]
    public void MarkUnpaid_OnGatewayPaidInvoice_ClearsSessionSoReconcilerCannotFindIt()
    {
        // An admin reversing a gateway-completed payment must leave no session behind: the
        // reconciler looks back 24 hours for pending gateway sessions, and a leftover session
        // here would let it re-query the gateway and silently re-mark the invoice Paid again,
        // undoing the admin's reversal.
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(14), currency: "USD", issuedAt: Now);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);
        invoice.Settle("bank-gateway", "gw-order-1", Now);

        invoice.MarkUnpaid();

        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Null(invoice.GatewayModule);
        Assert.Null(invoice.GatewayOrderId);
        Assert.Null(invoice.GatewayStartedAt);
        Assert.Null(invoice.GatewayTransactionId);
    }

    /// <summary>A payment recorded with a past date stamps the invoice as paid on that date.</summary>
    [Fact]
    public void AddPayment_WithPastDate_StampsPaidAtWithThatDate()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: 10m, quantity: 1);
        var paidOn = Now.AddMonths(-3);

        invoice.AddPayment(paidOn, "card", "txn-1", 10m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(paidOn);
    }

    /// <summary>Backdating moves both the creation and the issue date.</summary>
    [Fact]
    public void Backdate_MovesCreatedAtAndInvoiceDate()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        var earlier = Now.AddMonths(-5);

        invoice.Backdate(earlier);

        invoice.CreatedAt.Should().Be(earlier);
        invoice.InvoiceDate.Should().Be(earlier);
    }

    /// <summary>Imported totals keep the source's tax and credit, and the total follows them.</summary>
    [Fact]
    public void ImportTotals_KeepsSourceTaxAndCredit()
    {
        var invoice = UnpaidFor(100m);

        invoice.ImportTotals(tax: 7.25m, credit: 10m);

        invoice.Tax.Should().Be(7.25m);
        invoice.Credit.Should().Be(10m);
        invoice.Total.Should().Be(97.25m);
    }

    /// <summary>Imported totals are refused on an invoice that is already paid.</summary>
    [Fact]
    public void ImportTotals_WhenPaid_Throws()
    {
        var invoice = UnpaidFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.ImportTotals(tax: 1m, credit: 0m);

        act.Should().Throw<DomainRuleException>();
    }

    /// <summary>Builds an unpaid invoice for one line of <paramref name="price"/> USD.</summary>
    /// <param name="price">The line price.</param>
    /// <returns>The invoice.</returns>
    private static Invoice UnpaidFor(decimal price)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Hosting Plan", unitPrice: price, quantity: 1);
        return invoice;
    }

    /// <summary>The status predicates answer from the status alone.</summary>
    /// <param name="status">The status reached.</param>
    /// <param name="payable">Expected <see cref="Invoice.IsPayable"/>.</param>
    /// <param name="settled">Expected <see cref="Invoice.IsSettled"/>.</param>
    /// <param name="cancellable">Expected <see cref="Invoice.CanCancel"/>.</param>
    /// <param name="deletable">Expected <see cref="Invoice.CanDelete"/>.</param>
    [Theory]
    [InlineData(InvoiceStatus.Draft, false, false, true, true)]
    [InlineData(InvoiceStatus.Unpaid, true, false, true, false)]
    [InlineData(InvoiceStatus.Overdue, true, false, true, false)]
    [InlineData(InvoiceStatus.Paid, false, true, false, false)]
    [InlineData(InvoiceStatus.Refunded, false, true, false, false)]
    [InlineData(InvoiceStatus.Cancelled, false, false, false, true)]
    public void StatusPredicates_FollowTheStatus(
        InvoiceStatus status, bool payable, bool settled, bool cancellable, bool deletable)
    {
        var invoice = status == InvoiceStatus.Draft
            ? Invoice.CreateDraft(1, Now.AddDays(7), "USD", Now)
            : UnpaidFor(10m);
        switch (status)
        {
            case InvoiceStatus.Overdue:
                invoice.MarkOverdue(Now);
                break;
            case InvoiceStatus.Paid:
                invoice.RecordPaidHistory("ref", Now);
                break;
            case InvoiceStatus.Refunded:
                invoice.RecordPaidHistory("ref", Now);
                invoice.Refund();
                break;
            case InvoiceStatus.Cancelled:
                invoice.Cancel();
                break;
        }

        invoice.IsPayable.Should().Be(payable);
        invoice.IsSettled.Should().Be(settled);
        invoice.CanCancel.Should().Be(cancellable);
        invoice.CanDelete.Should().Be(deletable);
        Invoice.PayableFilter.Compile()(invoice).Should().Be(payable);
    }

    /// <summary>Settle marks the invoice paid and returns one ledger row for the amount due.</summary>
    [Fact]
    public void Settle_ReturnsLedgerRowForTheAmountDue()
    {
        var invoice = UnpaidFor(10m);
        invoice.AddPayment(Now, "Bank", "part", 4m, 0m, null);

        var row = invoice.Settle(PaymentMethodNames.Manual, "bulk-1", Now);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        row.Should().NotBeNull();
        row!.AmountIn.Should().Be(6m);
        row.PaymentMethod.Should().Be(PaymentMethodNames.Manual);
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().ContainSingle();
    }

    /// <summary>
    /// Settle records its own payment on the invoice, so after a partial payment and a settlement the
    /// invoice counts all the money it took and all of it can be refunded.
    /// </summary>
    [Fact]
    public void Settle_AfterPartialPayment_RecordsTheRestSoAllOfItIsRefundable()
    {
        var invoice = UnpaidFor(100m);
        invoice.AddPayment(Now, "Bank", "part", 30m, 0m, null);

        invoice.Settle(PaymentMethodNames.Card, "ch_rest", Now);

        invoice.Transactions.Where(t => t.Type == InvoiceTransactionType.Payment).Select(t => t.Amount)
            .Should().Equal(30m, 70m);
        invoice.AmountPaid.Should().Be(invoice.Total);
        invoice.RefundableAmount.Should().Be(invoice.Total);

        invoice.AddRefund(Now, "Bank", "refund-part", 30m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.RefundableAmount.Should().Be(70m);
    }

    /// <summary>ShareOfTotal gives a line its part of the total with the invoice's tax included.</summary>
    [Fact]
    public void ShareOfTotal_IncludesTheInvoiceTax()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: "USD", issuedAt: Now);
        invoice.AddItem("Domain", unitPrice: 25m, quantity: 1);
        invoice.AddItem("Hosting", unitPrice: 75m, quantity: 1);
        invoice.UpdateOptions(Now, Now.AddDays(7), null, taxRate: 20m);

        invoice.ShareOfTotal(25m).Should().Be(30m);
        invoice.ShareOfTotal(100m).Should().Be(invoice.Total);
    }

    /// <summary>ShareOfTotal is zero for an invoice with no subtotal.</summary>
    [Fact]
    public void ShareOfTotal_WithNoSubtotal_IsZero()
    {
        var invoice = UnpaidFor(0m);

        invoice.ShareOfTotal(10m).Should().Be(0m);
    }

    /// <summary>Settling a zero-total invoice writes no ledger row.</summary>
    [Fact]
    public void Settle_WithNothingDue_ReturnsNoLedgerRow()
    {
        var invoice = UnpaidFor(0m);

        var row = invoice.Settle(PaymentMethodNames.Manual, "bulk-1", Now);

        row.Should().BeNull();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>
    /// A settlement through another path keeps the stored gateway session, so a payment it takes later
    /// is still matched; the invoice reads as paid through the path that took the money, not the session.
    /// </summary>
    [Fact]
    public void Settle_ThroughAnotherPath_KeepsTheSessionButNotAsThePayer()
    {
        var invoice = UnpaidFor(10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);

        invoice.Settle(PaymentMethodNames.Card, "ch_1", Now);

        invoice.GatewayModule.Should().Be("bank-gateway");
        invoice.GatewayOrderId.Should().Be("gw-order-1");
        invoice.WasPaidThrough("bank-gateway").Should().BeFalse();
        invoice.WasPaidThrough(PaymentMethodNames.Card).Should().BeTrue();
        invoice.RefundableThrough("bank-gateway").Should().Be(0m);
    }

    /// <summary>An admin payment covering an invoice with an open gateway session keeps the session and is not refundable through it.</summary>
    [Fact]
    public void AddPayment_CoveringWithOpenSession_KeepsTheSessionAndRoutesNothingThroughIt()
    {
        var invoice = UnpaidFor(10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);

        invoice.AddPayment(Now, PaymentMethodNames.Manual, "manual-1", 10m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.GatewayOrderId.Should().Be("gw-order-1");
        invoice.WasPaidThrough("bank-gateway").Should().BeFalse();
        invoice.RefundableThrough(PaymentMethodNames.Manual).Should().Be(10m);
    }

    /// <summary>
    /// The settled-with-unrecorded-session filter matches an invoice another path settled while its
    /// session stayed open, and stops matching once a payment is recorded through that session.
    /// </summary>
    [Fact]
    public void SettledWithUnrecordedSessionFilter_MatchesOnlyUntilTheSessionPays()
    {
        var filter = Invoice.SettledWithUnrecordedSessionFilter.Compile();
        var otherPath = UnpaidFor(10m);
        otherPath.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);
        var throughSession = UnpaidFor(10m);
        throughSession.SetGatewaySession("bank-gateway", "gw-order-2", GatewayAttemptAt);

        var openBefore = filter(otherPath);
        otherPath.Settle(PaymentMethodNames.Manual, "manual-1", Now);
        throughSession.Settle("bank-gateway", "gw-order-2", Now);

        openBefore.Should().BeFalse();
        filter(otherPath).Should().BeTrue();
        filter(throughSession).Should().BeFalse();
    }

    /// <summary>Settling through the gateway session itself records it as the payer.</summary>
    [Fact]
    public void Settle_ThroughTheSession_RecordsItAsThePayer()
    {
        var invoice = UnpaidFor(10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-1", GatewayAttemptAt);

        invoice.Settle("bank-gateway", "gw-txn-1", Now);

        invoice.WasPaidThrough("bank-gateway").Should().BeTrue();
        invoice.RefundableThrough("bank-gateway").Should().Be(10m);
    }

    /// <summary>A partial payment keeps the invoice open; the payment that covers the rest marks it paid.</summary>
    [Fact]
    public void AddPayment_Partial_KeepsInvoiceOpenUntilCovered()
    {
        var invoice = UnpaidFor(10m);

        var first = invoice.AddPayment(Now, "Bank", "tx1", 4m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.AmountDue.Should().Be(6m);
        first.AmountIn.Should().Be(4m);
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().BeEmpty();

        invoice.AddPayment(Now, "Bank", "tx2", 6m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.IsPaidInFull.Should().BeTrue();
        invoice.DomainEvents.OfType<PaymentReceivedEvent>().Should().ContainSingle();
    }

    /// <summary>A partial payment on an overdue invoice leaves it Overdue, because the debt is still late.</summary>
    [Fact]
    public void AddPayment_Partial_OnOverdueInvoice_LeavesItOverdue()
    {
        var invoice = UnpaidFor(10m);
        invoice.MarkOverdue(Now.AddDays(8));

        invoice.AddPayment(Now.AddDays(8), "Bank", "tx1", 4m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Overdue);
        invoice.AmountDue.Should().Be(6m);
        invoice.PaidAt.Should().BeNull();
    }

    /// <summary>A payment on a paid invoice refuses with <c>INVOICE_NOT_PAYABLE</c>.</summary>
    [Fact]
    public void AddPayment_OnPaidInvoice_ThrowsWithInvoiceNotPayable()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.AddPayment(Now, "Bank", "tx", 1m, 0m, null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotPayable);
    }

    /// <summary>A partial refund leaves the invoice PartiallyRefunded; refunding the rest marks it Refunded.</summary>
    [Fact]
    public void AddRefund_Partial_LeavesInvoicePartiallyRefundedUntilFullyRefunded()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        var row = invoice.AddRefund(Now, "Manual", "r1", 3m, 0m, null, addedToCredit: true);

        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        invoice.RefundableAmount.Should().Be(7m);
        row.AmountOut.Should().Be(3m);
        row.AddedToCredit.Should().BeTrue();

        invoice.AddRefund(Now, "Manual", "r2", 7m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>A refund larger than what is still refundable refuses with <c>INVOICE_REFUND_EXCEEDS_PAID</c>.</summary>
    [Fact]
    public void AddRefund_MoreThanPaid_ThrowsWithRefundExceedsPaid()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.AddRefund(Now, "Manual", "r1", 11m, 0m, null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceRefundExceedsPaid);
        invoice.Transactions.Should().BeEmpty();
    }

    /// <summary>Credit beyond what the invoice still owes refuses with <c>INVOICE_CREDIT_EXCEEDS_DUE</c>.</summary>
    [Fact]
    public void ApplyCredit_MoreThanDue_ThrowsWithCreditExceedsDue()
    {
        var invoice = UnpaidFor(10m);

        var act = () => invoice.ApplyCredit(11m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceCreditExceedsDue);
        invoice.Credit.Should().Be(0m);
    }

    /// <summary>RemoveCredit never removes more than the invoice holds and reports what it removed.</summary>
    [Fact]
    public void RemoveCredit_CapsAtTheCreditHeld()
    {
        var invoice = UnpaidFor(10m);
        invoice.ApplyCredit(4m);

        var removed = invoice.RemoveCredit(9m);

        removed.Should().Be(4m);
        invoice.Credit.Should().Be(0m);
        invoice.Total.Should().Be(10m);
    }

    /// <summary>Cancelling an already cancelled invoice refuses with <c>INVOICE_CANCEL_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsWithInvoiceCancelNotAllowed()
    {
        var invoice = UnpaidFor(10m);
        invoice.Cancel();

        var act = () => invoice.Cancel();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceCancelNotAllowed);
    }

    /// <summary>MarkPaid after a partial payment refuses with <c>INVOICE_AMOUNT_STILL_DUE</c>, so a Paid invoice is always paid in full.</summary>
    [Fact]
    public void MarkPaid_AfterPartialPayment_ThrowsWithAmountStillDue()
    {
        var invoice = UnpaidFor(100m);
        invoice.AddPayment(Now, "Bank", "tx1", 40m, 0m, null);

        var act = () => invoice.MarkPaid("ref", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceAmountStillDue);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>Settle after a partial payment records the rest, so the invoice ends Paid in full.</summary>
    [Fact]
    public void Settle_AfterPartialPayment_IsPaidInFull()
    {
        var invoice = UnpaidFor(100m);
        invoice.AddPayment(Now, "Bank", "tx1", 40m, 0m, null);

        var row = invoice.Settle("Bank", "tx2", Now);

        row!.AmountIn.Should().Be(60m);
        invoice.IsPaidInFull.Should().BeTrue();
        invoice.AmountPaid.Should().Be(100m);
    }

    /// <summary>RecordPaidHistory closes an imported invoice without a payment row and counts the whole total as paid.</summary>
    [Fact]
    public void RecordPaidHistory_CountsTotalAsPaid()
    {
        var invoice = UnpaidFor(100m);

        invoice.RecordPaidHistory("migrated", Now);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Transactions.Should().BeEmpty();
        invoice.IsPaidInFull.Should().BeTrue();
    }

    /// <summary>A payment above what is still owed refuses with <c>INVOICE_PAYMENT_EXCEEDS_DUE</c> and records nothing.</summary>
    [Fact]
    public void AddPayment_AboveAmountDue_ThrowsWithPaymentExceedsDue()
    {
        var invoice = UnpaidFor(100m);

        var act = () => invoice.AddPayment(Now, "Bank", "tx1", 150m, 0m, null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoicePaymentExceedsDue);
        invoice.Transactions.Should().BeEmpty();
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>Cancelling an invoice that already took a payment refuses with <c>INVOICE_HAS_PAYMENTS</c>.</summary>
    [Fact]
    public void Cancel_WithRecordedPayment_ThrowsWithHasPayments()
    {
        var invoice = UnpaidFor(100m);
        invoice.AddPayment(Now, "Bank", "tx1", 40m, 0m, null);

        var act = () => invoice.Cancel();

        invoice.CanCancel.Should().BeFalse();
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceHasPayments);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>Credit cannot be taken off a Paid invoice: <c>INVOICE_NOT_EDITABLE</c>, credit and total unchanged.</summary>
    [Fact]
    public void RemoveCredit_OnPaidInvoice_ThrowsWithNotEditable()
    {
        var invoice = UnpaidFor(100m);
        invoice.ApplyCredit(100m);
        invoice.MarkPaid("credit", Now);

        var act = () => invoice.RemoveCredit(100m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.Credit.Should().Be(100m);
        invoice.Total.Should().Be(0m);
    }

    /// <summary>Lines of a Paid invoice cannot be removed: <c>INVOICE_NOT_EDITABLE</c>, total unchanged.</summary>
    [Fact]
    public void RemoveItem_OnPaidInvoice_ThrowsWithNotEditable()
    {
        var invoice = UnpaidFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.RemoveItem(invoice.Items[0].Id);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.Total.Should().Be(100m);
    }

    /// <summary>Lines of a Paid invoice cannot be changed: <c>INVOICE_NOT_EDITABLE</c>, total unchanged.</summary>
    [Fact]
    public void UpdateItem_OnPaidInvoice_ThrowsWithNotEditable()
    {
        var invoice = UnpaidFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var act = () => invoice.UpdateItem(invoice.Items[0].Id, "Cheaper", 1m, 1);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.Total.Should().Be(100m);
    }

    /// <summary>The tax rate of a Paid invoice cannot change, while its dates still can.</summary>
    [Fact]
    public void UpdateOptions_OnPaidInvoice_RefusesTaxRateButKeepsDatesEditable()
    {
        var invoice = UnpaidFor(100m);
        invoice.RecordPaidHistory("ref", Now);

        var changeRate = () => invoice.UpdateOptions(Now, Now.AddDays(1), null, 20m);
        invoice.UpdateOptions(Now, Now.AddDays(2), "Bank", 0m);

        changeRate.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceNotEditable);
        invoice.Total.Should().Be(100m);
        invoice.DueDate.Should().Be(Now.AddDays(2));
    }

    /// <summary>A paid invoice whose payment is recorded cannot be reopened: <c>INVOICE_HAS_PAYMENTS</c>, still Paid.</summary>
    [Fact]
    public void MarkUnpaid_WithRecordedPayment_ThrowsWithHasPayments()
    {
        var invoice = UnpaidFor(10m);
        invoice.AddPayment(Now, "Bank", "full", 10m, 0m, null);

        var act = invoice.MarkUnpaid;

        invoice.CanMarkUnpaid.Should().BeFalse();
        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceHasPayments);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>An invoice closed as history, with no payment row, can be reopened and owes its total again.</summary>
    [Fact]
    public void MarkUnpaid_WithoutRecordedPayment_ReopensTheInvoice()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        invoice.CanMarkUnpaid.Should().BeTrue();
        invoice.MarkUnpaid();

        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.AmountDue.Should().Be(10m);
    }

    /// <summary>An invoice that already took a payment here cannot be closed as history: <c>INVOICE_HAS_PAYMENTS</c>.</summary>
    [Fact]
    public void RecordPaidHistory_AfterPartialPayment_ThrowsWithHasPayments()
    {
        var invoice = UnpaidFor(10m);
        invoice.AddPayment(Now, "Bank", "part", 4m, 0m, null);

        var act = () => invoice.RecordPaidHistory("ref", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceHasPayments);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>An imported invoice that is already Refunded offers nothing more to refund.</summary>
    [Fact]
    public void RefundableAmount_OnRefundedInvoice_IsZero()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        invoice.Refund();

        invoice.RefundableAmount.Should().Be(0m);
    }

    /// <summary>Refunding keeps the moment the invoice was paid; the Refunded status says it was given back.</summary>
    [Fact]
    public void Refund_KeepsPaidAt()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        invoice.Refund();

        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.PaidAt.Should().Be(Now);
    }

    /// <summary>A full refund recorded through AddRefund turns the invoice Refunded and keeps when it was paid.</summary>
    [Fact]
    public void AddRefund_Full_KeepsPaidAt()
    {
        var invoice = UnpaidFor(10m);
        invoice.RecordPaidHistory("ref", Now);

        invoice.AddRefund(Now, "Manual", "r1", 10m, 0m, null);

        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.PaidAt.Should().Be(Now);
    }

    /// <summary>Removing a line that would leave less than the payments taken is refused with <c>INVOICE_TOTAL_BELOW_PAID</c>.</summary>
    [Fact]
    public void RemoveItem_BelowPaymentsTaken_ThrowsWithTotalBelowPaid()
    {
        var invoice = UnpaidFor(10m);
        invoice.AddPayment(Now, "Bank", "part", 4m, 0m, null);

        var act = () => invoice.RemoveItem(invoice.Items[0].Id);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTotalBelowPaid);
        invoice.Items.Should().ContainSingle();
        invoice.Total.Should().Be(10m);
    }

    /// <summary>Lowering a line below the payments taken is refused; a change that still covers them is allowed.</summary>
    [Fact]
    public void UpdateItem_BelowPaymentsTaken_ThrowsWithTotalBelowPaid()
    {
        var invoice = UnpaidFor(10m);
        invoice.AddPayment(Now, "Bank", "part", 4m, 0m, null);
        var itemId = invoice.Items[0].Id;

        var act = () => invoice.UpdateItem(itemId, "Cheaper", 3m, 1);
        invoice.UpdateItem(itemId, "Still covered", 4m, 1);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTotalBelowPaid);
        invoice.Total.Should().Be(4m);
    }

    /// <summary>A tax cut that would leave less than the payments taken is refused with <c>INVOICE_TOTAL_BELOW_PAID</c>.</summary>
    [Fact]
    public void UpdateOptions_TaxCutBelowPaymentsTaken_ThrowsWithTotalBelowPaid()
    {
        var invoice = UnpaidFor(100m);
        invoice.UpdateOptions(Now, Now.AddDays(7), null, taxRate: 10m);
        invoice.AddPayment(Now, "Bank", "part", 105m, 0m, null);

        var act = () => invoice.UpdateOptions(Now, Now.AddDays(7), null, taxRate: 0m);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(BillingErrors.InvoiceTotalBelowPaid);
        invoice.Total.Should().Be(110m);
    }

    /// <summary>Lowering a line below the applied credit leaves exactly the unneeded credit as <see cref="Invoice.ExcessCredit"/>.</summary>
    [Fact]
    public void ExcessCredit_AfterLineFallsBelowCredit_IsTheUnneededCredit()
    {
        var invoice = UnpaidFor(10m);
        invoice.ApplyCredit(8m);

        invoice.UpdateItem(invoice.Items[0].Id, "Cheaper", 5m, 1);

        invoice.ExcessCredit.Should().Be(3m);
        invoice.RemoveCredit(invoice.ExcessCredit);
        invoice.Total.Should().Be(0m);
        invoice.ExcessCredit.Should().Be(0m);
    }

    /// <summary>Credit the invoice still needs above the payments taken is not excess.</summary>
    [Fact]
    public void ExcessCredit_WhileTheTotalCoversThePayments_IsZero()
    {
        var invoice = UnpaidFor(10m);
        invoice.ApplyCredit(5m);
        invoice.AddPayment(Now, "Bank", "part", 2m, 0m, null);

        invoice.UpdateItem(invoice.Items[0].Id, "Cheaper", 7m, 1);

        invoice.ExcessCredit.Should().Be(0m);
        invoice.Total.Should().Be(2m);
    }

    /// <summary>Tax and total are rounded to the currency's minor unit, half away from zero.</summary>
    /// <param name="currency">The invoice currency.</param>
    /// <param name="price">The line price, invariant text so it reaches the test as an exact decimal.</param>
    /// <param name="taxRate">The tax percentage, invariant text.</param>
    /// <param name="expectedTax">The rounded tax, invariant text.</param>
    [Theory]
    [InlineData("USD", "10.01", "7.5", "0.75")]
    [InlineData("USD", "10.10", "5", "0.51")]
    [InlineData("JPY", "1001", "10", "100")]
    [InlineData("KWD", "1.001", "5", "0.050")]
    public void UpdateOptions_RoundsTaxToTheCurrencyMinorUnit(string currency, string price, string taxRate, string expectedTax)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: Now.AddDays(7), currency: currency, issuedAt: Now);
        var lineAmount = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        invoice.AddItem("Hosting Plan", unitPrice: lineAmount, quantity: 1);

        invoice.UpdateOptions(Now, Now.AddDays(7), null, decimal.Parse(taxRate, System.Globalization.CultureInfo.InvariantCulture));

        var tax = decimal.Parse(expectedTax, System.Globalization.CultureInfo.InvariantCulture);
        invoice.Tax.Should().Be(tax);
        invoice.Total.Should().Be(lineAmount + tax);
    }
}
