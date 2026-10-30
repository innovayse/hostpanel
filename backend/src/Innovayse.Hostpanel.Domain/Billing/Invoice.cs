using System.Linq.Expressions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// An invoice issued to a client for services or products.
/// Owns a collection of <see cref="InvoiceItem"/> line items and <see cref="InvoiceTransaction"/> records.
/// Stored in the <c>Invoices</c> table.
/// </summary>
public sealed class Invoice : BaseEntity, IAggregateRoot
{
    /// <summary>Internal mutable list of line items.</summary>
    private readonly List<InvoiceItem> _items = [];

    /// <summary>Internal mutable list of transactions.</summary>
    private readonly List<InvoiceTransaction> _transactions = [];

    /// <summary>
    /// How long an unanswered refund claim blocks the next refund. Past it the claim counts as
    /// abandoned (the process died between the gateway call and the save), so the invoice is not
    /// locked forever; an operator checks the gateway before refunding it again.
    /// </summary>
    public static readonly TimeSpan RefundClaimLease = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The statuses of an invoice that was paid and still holds at least part of that money: Paid,
    /// and PartiallyRefunded after a refund that did not reach the whole amount. The one list a query
    /// filters on (<c>PaidStatuses.Contains(i.Status)</c>) and <see cref="IsPaid"/> reads, so a report,
    /// a renewal sweep and the entity itself never disagree on which invoices count as paid.
    /// </summary>
    public static readonly InvoiceStatus[] PaidStatuses = [InvoiceStatus.Paid, InvoiceStatus.PartiallyRefunded];

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>Gets the invoice issue date (UTC).</summary>
    public DateTimeOffset InvoiceDate { get; private set; }

    /// <summary>Gets the payment due date (UTC).</summary>
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>Gets the UTC timestamp when the invoice was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when payment was received; null if never paid. A historical fact: it
    /// stays when the invoice is refunded, so read <see cref="Status"/> to know whether it is paid now.
    /// </summary>
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>
    /// Gets when the invoice was marked lapsed (UTC): it stayed overdue past its grace period, so the
    /// period it billed was never paid for. <see langword="null"/> while it has not lapsed. A historical
    /// fact like <see cref="PaidAt"/>: a lapsed invoice paid later keeps it, and <see cref="Status"/> says
    /// whether it is paid now.
    /// </summary>
    public DateTimeOffset? LapsedAt { get; private set; }

    /// <summary>
    /// Gets whether at least one line charges for a digital product (<see cref="InvoiceItem.IsDigital"/>).
    /// Stored rather than read from the lines, so the fulfilment path can tell from the invoice row alone
    /// whether plugins have anything to hear about; recomputed with the totals whenever a line changes.
    /// </summary>
    public bool HasDigitalItems { get; private set; }

    /// <summary>Gets the sum of all line item amounts before tax and credit.</summary>
    public decimal SubTotal { get; private set; }

    /// <summary>Gets the tax rate percentage applied to the invoice.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Gets the computed tax amount (SubTotal × TaxRate / 100).</summary>
    public decimal Tax { get; private set; }

    /// <summary>Gets the total credit applied to the invoice.</summary>
    public decimal Credit { get; private set; }

    /// <summary>Gets the final total (SubTotal + Tax − Credit).</summary>
    public decimal Total { get; private set; }

    /// <summary>
    /// ISO 4217 alpha code every amount on this invoice is in. Set at creation, never changed.
    /// </summary>
    /// <remarks>
    /// Inferring the currency from the client at payment time would let a change of the client's
    /// currency silently re-label every past invoice: $100 would become ֏100. The number and its
    /// currency travel together.
    /// </remarks>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the payment gateway transaction reference; null until paid.</summary>
    public string? GatewayTransactionId { get; private set; }

    /// <summary>Gets the external system ID (e.g. source system invoice ID) used for migration deduplication.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Gets the payment plugin id that initiated the latest gateway payment session; null when none.</summary>
    public string? GatewayModule { get; private set; }

    /// <summary>Gets the gateway-side order id of the latest payment attempt; null when none.</summary>
    public string? GatewayOrderId { get; private set; }

    /// <summary>Gets the UTC timestamp when the latest gateway payment attempt started; null when none.</summary>
    public DateTimeOffset? GatewayStartedAt { get; private set; }

    /// <summary>Gets the UTC moment the reconciler last asked the gateway about the latest session; null when it has not yet.</summary>
    public DateTimeOffset? GatewayCheckedAt { get; private set; }

    /// <summary>Gets how many times the reconciler has asked the gateway about the latest session; it sets the back-off before the next ask.</summary>
    public int GatewayCheckCount { get; private set; }

    /// <summary>
    /// Gets the UTC moment the gateway gave a final answer that left the invoice unpaid (the session was
    /// declined or expired); null while the session is still open. A concluded session is not asked about again.
    /// </summary>
    public DateTimeOffset? GatewayConcludedAt { get; private set; }

    /// <summary>Gets the UTC moment staff were told the latest session stayed pending for an unusual time; null when they were not, so they are told once per session.</summary>
    public DateTimeOffset? GatewayStalledNotifiedAt { get; private set; }

    /// <summary>
    /// Gets when the latest refund began (UTC), or null when none has or an unanswered one was
    /// released. Every refund writes it, so two refunds racing on the same invoice always change the
    /// same row and the later save fails the invoice's concurrency check; a refund that is still
    /// waiting for the gateway holds it as a claim (<see cref="IsRefundInFlight"/>).
    /// </summary>
    public DateTimeOffset? RefundStartedAt { get; private set; }

    /// <summary>Gets optional notes for this invoice.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the preferred payment method; null when not specified.</summary>
    public string? PaymentMethod { get; private set; }

    /// <summary>Gets the read-only view of invoice line items.</summary>
    public IReadOnlyList<InvoiceItem> Items => _items.AsReadOnly();

    /// <summary>Gets the read-only view of invoice transactions.</summary>
    public IReadOnlyList<InvoiceTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>
    /// Gets the filter form of <see cref="IsPayable"/> for queries, so a database read and an
    /// in-memory check can never disagree on which invoices are open for payment.
    /// </summary>
    public static Expression<Func<Invoice, bool>> PayableFilter =>
        i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Overdue;

    /// <summary>
    /// Gets the filter for settled invoices that still hold a gateway session no payment was
    /// recorded through (<see cref="IsSettled"/> and not <see cref="WasPaidThrough"/> for the
    /// session's module): another path settled them while the session was open, so the session
    /// may still have taken money the ledger does not hold.
    /// </summary>
    public static Expression<Func<Invoice, bool>> SettledWithUnrecordedSessionFilter =>
        i => (PaidStatuses.Contains(i.Status) || i.Status == InvoiceStatus.Refunded)
            && i.GatewayModule != null
            && i.GatewayOrderId != null
            && !i.Transactions.Any(t => t.Type == InvoiceTransactionType.Payment && t.Gateway == i.GatewayModule);

    /// <summary>
    /// Gets the filter for the invoices a client may see: every invoice except a draft, which is
    /// work in progress on the staff side until it is issued. Every client-facing read and the
    /// ownership check apply this one filter, so a draft answers like an invoice that does not exist.
    /// </summary>
    public static Expression<Func<Invoice, bool>> ClientVisibleFilter =>
        i => i.Status != InvoiceStatus.Draft;

    /// <summary>Gets whether the invoice is open for payment: Unpaid or Overdue.</summary>
    public bool IsPayable => Status is InvoiceStatus.Unpaid or InvoiceStatus.Overdue;

    /// <summary>
    /// Gets whether the invoice was paid and still holds at least part of that money: Paid or
    /// PartiallyRefunded (<see cref="PaidStatuses"/>). Only such an invoice can be refunded.
    /// </summary>
    public bool IsPaid => PaidStatuses.Contains(Status);

    /// <summary>Gets whether the invoice's money question is closed: Paid, PartiallyRefunded or Refunded.</summary>
    public bool IsSettled => IsPaid || Status == InvoiceStatus.Refunded;

    /// <summary>
    /// Gets whether <see cref="MarkLapsed"/> would mark the invoice now: it is Overdue and has not lapsed yet.
    /// A sweep asks this before marking, so an invoice is never reported lapsed twice.
    /// </summary>
    public bool CanLapse => Status == InvoiceStatus.Overdue && LapsedAt is null;

    /// <summary>
    /// Gets whether <see cref="Cancel"/> is allowed: Draft, Unpaid or Overdue, and no payment
    /// recorded yet. Money already taken would be stranded on a cancelled invoice, so a part-paid
    /// invoice is refunded or paid in full, never cancelled.
    /// </summary>
    public bool CanCancel =>
        Status is (InvoiceStatus.Draft or InvoiceStatus.Unpaid or InvoiceStatus.Overdue) && PaymentsRecorded == 0;

    /// <summary>
    /// Gets whether <see cref="MarkUnpaid"/> is allowed: Paid, and no payment recorded. Recorded
    /// payments are undone by deleting or refunding them, never by reopening the invoice.
    /// </summary>
    public bool CanMarkUnpaid => Status == InvoiceStatus.Paid && PaymentsRecorded == 0;

    /// <summary>Gets whether the invoice may be deleted: only a Draft or a Cancelled one, which took no money.</summary>
    public bool CanDelete => Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled;

    /// <summary>Gets the sum of the payments recorded through <see cref="AddPayment"/> and <see cref="Settle"/>.</summary>
    public decimal PaymentsRecorded => SumOf(InvoiceTransactionType.Payment);

    /// <summary>Gets the sum of the refunds recorded through <see cref="AddRefund"/>.</summary>
    public decimal AmountRefunded => SumOf(InvoiceTransactionType.Refund);

    /// <summary>
    /// Gets how much money the invoice took: the recorded payments when there are any, otherwise
    /// the whole <see cref="Total"/> once settled, because <see cref="MarkPaid"/> (credit already
    /// booked) and <see cref="RecordPaidHistory"/> (imported history) close the invoice without a payment row. <see cref="Settle"/>
    /// records its payment row, so money it takes is always counted here.
    /// </summary>
    public decimal AmountPaid
    {
        get
        {
            var recorded = PaymentsRecorded;
            if (recorded > 0)
            {
                return recorded;
            }

            return IsSettled ? Total : 0m;
        }
    }

    /// <summary>
    /// Gets what is still owed: <see cref="Total"/> (already net of credit) less the recorded
    /// payments, never negative, while the invoice can still take money. A closed invoice (Paid,
    /// PartiallyRefunded, Refunded or Cancelled) owes nothing, whatever its transactions add up to: one closed without a
    /// payment row (credit, imported history) would otherwise show its whole total as owed.
    /// Every path that settles the invoice charges this, not the total, so a payment recorded
    /// earlier is never charged again.
    /// </summary>
    public decimal AmountDue => IsClosed ? 0m : Math.Max(0m, Total - PaymentsRecorded);

    /// <summary>Gets whether the invoice's money question is over and nothing more is owed: Paid, PartiallyRefunded, Refunded or Cancelled.</summary>
    private bool IsClosed => IsSettled || Status == InvoiceStatus.Cancelled;

    /// <summary>
    /// Gets what can still be refunded: <see cref="AmountPaid"/> less <see cref="AmountRefunded"/>
    /// while the invoice <see cref="IsPaid"/> (Paid or PartiallyRefunded), otherwise zero. Only such an
    /// invoice is refunded, and an imported Refunded invoice has no refund rows, so without the status
    /// rule it would offer its whole total again. This is the cap every refund is held to: what was
    /// paid less every earlier refund, so two partial refunds in a row can never return more than was paid.
    /// </summary>
    public decimal RefundableAmount => IsPaid ? Math.Max(0m, AmountPaid - AmountRefunded) : 0m;

    /// <summary>
    /// Gets the credit the invoice holds beyond what it still needs: the amount <see cref="Total"/>
    /// has fallen below <see cref="PaymentsRecorded"/> after a line or the tax rate came down, never
    /// more than <see cref="Credit"/>. The caller returns it to the client's balance in the same save.
    /// </summary>
    public decimal ExcessCredit => Math.Min(Credit, Math.Max(0m, PaymentsRecorded - Total));

    /// <summary>
    /// Gets whether a payment was recorded on this invoice through one gateway: the test of whether
    /// that gateway took money here, since a stored session alone does not say so.
    /// </summary>
    /// <param name="gateway">The gateway or payment method name the payments were recorded under.</param>
    /// <returns><see langword="true"/> when at least one payment was recorded through it.</returns>
    public bool WasPaidThrough(string gateway) =>
        _transactions.Any(t => t.Type == InvoiceTransactionType.Payment && t.Gateway == gateway);

    /// <summary>
    /// Gets what one gateway can still give back: the payments recorded through it less the refunds
    /// already made through it, never more than <see cref="RefundableAmount"/>.
    /// </summary>
    /// <param name="gateway">The gateway or payment method name the payments were recorded under.</param>
    /// <returns>The amount still refundable through that gateway, never negative.</returns>
    public decimal RefundableThrough(string gateway)
    {
        var paid = _transactions.Where(t => t.Type == InvoiceTransactionType.Payment && t.Gateway == gateway).Sum(t => t.Amount);
        var refunded = _transactions.Where(t => t.Type == InvoiceTransactionType.Refund && t.Gateway == gateway).Sum(t => t.Amount);
        return Math.Min(RefundableAmount, Math.Max(0m, paid - refunded));
    }

    /// <summary>
    /// Gets whether a refund has claimed the invoice and is still waiting for its gateway: the claim
    /// in <see cref="RefundStartedAt"/> is younger than <see cref="RefundClaimLease"/> and no refund
    /// has been recorded since it began.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns><see langword="true"/> while another refund must wait.</returns>
    public bool IsRefundInFlight(DateTimeOffset now) =>
        RefundStartedAt is { } started
        && now - started < RefundClaimLease
        && !_transactions.Any(t => t.Type == InvoiceTransactionType.Refund && t.Date >= started);

    /// <summary>
    /// Gets whether the invoice was paid (<see cref="IsPaid"/>) and the money covers it. A partial
    /// refund since does not undo it: the payment covered the invoice, and a refund of one line (a
    /// domain the registrar refused) leaves the other lines paid for.
    /// </summary>
    public bool IsPaidInFull => IsPaid && AmountPaid >= Total;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Invoice() { }

    /// <summary>Creates a draft invoice (not yet sent to the client).</summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="currency">ISO 4217 alpha code the invoice bills in.</param>
    /// <param name="issuedAt">When the invoice is issued (UTC); becomes the invoice date and the creation event moment.</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Draft"/> status.</returns>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    public static Invoice CreateDraft(int clientId, DateTimeOffset dueDate, string currency, DateTimeOffset issuedAt)
    {
        var invoice = new Invoice
        {
            ClientId = clientId,
            Currency = CurrencyCode.Normalise(currency),
            Status = InvoiceStatus.Draft,
            InvoiceDate = issuedAt,
            DueDate = dueDate,
        };
        // Built when the event is dispatched, not now: the id is assigned on save and the items,
        // so the total, are added after creation.
        invoice.AddDomainEvent(() => new InvoiceCreatedEvent(invoice.Id, clientId, invoice.Total, invoice.Currency) { OccurredAt = issuedAt });
        return invoice;
    }

    /// <summary>Creates an unpaid invoice.</summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="currency">ISO 4217 alpha code the invoice bills in.</param>
    /// <param name="issuedAt">When the invoice is issued (UTC); becomes the invoice date and the creation event moment.</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Unpaid"/> status.</returns>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    public static Invoice Create(int clientId, DateTimeOffset dueDate, string currency, DateTimeOffset issuedAt)
    {
        // Same construction and the same InvoiceCreatedEvent as a draft; only the status differs.
        var invoice = CreateDraft(clientId, dueDate, currency, issuedAt);
        invoice.Status = InvoiceStatus.Unpaid;
        return invoice;
    }

    /// <summary>Creates a draft or unpaid invoice.</summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="currency">ISO 4217 alpha code the invoice bills in.</param>
    /// <param name="isDraft">When true, creates a Draft; otherwise creates an Unpaid invoice.</param>
    /// <param name="issuedAt">When the invoice is issued (UTC); becomes the invoice date and the creation event moment.</param>
    /// <returns>A new <see cref="Invoice"/>.</returns>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    public static Invoice Create(int clientId, DateTimeOffset dueDate, string currency, bool isDraft, DateTimeOffset issuedAt) =>
        isDraft ? CreateDraft(clientId, dueDate, currency, issuedAt) : Create(clientId, dueDate, currency, issuedAt);

    /// <summary>Creates an unpaid invoice with one single-quantity line per billable item.</summary>
    /// <param name="clientId">FK to the client being invoiced.</param>
    /// <param name="dueDate">Payment due date (UTC).</param>
    /// <param name="currency">ISO 4217 alpha code the invoice bills in.</param>
    /// <param name="items">The billable items to charge, one line each at the item's amount.</param>
    /// <param name="issuedAt">When the invoice is issued (UTC); becomes the invoice date and the creation event moment.</param>
    /// <returns>A new <see cref="Invoice"/> with <see cref="InvoiceStatus.Unpaid"/> status and the lines added.</returns>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    public static Invoice CreateForBillableItems(
        int clientId, DateTimeOffset dueDate, string currency, IEnumerable<BillableItem> items, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(items);

        var invoice = Create(clientId, dueDate, currency, issuedAt);
        foreach (var item in items)
        {
            invoice.AddItem(item.Description, item.Amount, 1);
        }

        return invoice;
    }

    /// <summary>Sets the external system ID for migration deduplication.</summary>
    /// <param name="externalId">Identifier of this invoice in the system it was imported from.</param>
    public void SetExternalId(string externalId) => ExternalId = externalId;

    /// <summary>
    /// Publishes a draft invoice, transitioning it to Unpaid status.
    /// </summary>
    /// <exception cref="DomainRuleException">Thrown when the invoice is not in Draft status.</exception>
    public void Publish()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotDraft,
                $"Only Draft invoices can be published; current status is {Status}.");
        }

        Status = InvoiceStatus.Unpaid;
    }

    /// <summary>
    /// Adds a line item and recalculates totals.
    /// </summary>
    /// <param name="description">Human-readable charge description.</param>
    /// <param name="unitPrice">Price per unit.</param>
    /// <param name="quantity">Number of units.</param>
    /// <param name="clientServiceId">
    /// FK to the <c>ClientService</c> this line is charged for, or <see langword="null"/> when
    /// it is not one service's charge or the caller does not know which. Only a caller holding
    /// the service itself may pass it: see <see cref="InvoiceItem.ClientServiceId"/> for why
    /// inferring it from the description is not allowed.
    /// </param>
    /// <param name="productSnapshot">
    /// What the product this line charges for is at billing time, or <see langword="null"/> when the line
    /// is not a product's charge; see <see cref="InvoiceItem.ProductSnapshot"/>.
    /// </param>
    /// <exception cref="DomainRuleException">Thrown when the invoice is no longer editable.</exception>
    public void AddItem(
        string description, decimal unitPrice, int quantity, int? clientServiceId = null, ProductSnapshot? productSnapshot = null)
    {
        EnsureEditable();

        var item = InvoiceItem.Create(description, unitPrice, quantity, clientServiceId, productSnapshot);
        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>Adds a renewal line for one service and one period, and recalculates totals.</summary>
    /// <param name="description">Line description.</param>
    /// <param name="amount">Price of the period.</param>
    /// <param name="clientServiceId">The service renewed.</param>
    /// <param name="start">Period start.</param>
    /// <param name="end">Period end.</param>
    /// <param name="productSnapshot">
    /// What the renewed service's product is at billing time, so the renewal is fulfilled as what is sold
    /// now; <see langword="null"/> only when the caller has no product for the service.
    /// </param>
    /// <exception cref="DomainRuleException">The invoice is no longer editable.</exception>
    public void AddServiceRenewal(
        string description, decimal amount, int clientServiceId, DateTimeOffset start, DateTimeOffset end,
        ProductSnapshot? productSnapshot = null) =>
        AddRenewal(InvoiceItem.CreateRenewal(description, amount, clientServiceId, null, start, end, productSnapshot));

    /// <summary>Adds a renewal line for one domain and one period, and recalculates totals.</summary>
    /// <param name="description">Line description.</param>
    /// <param name="amount">Price of the period.</param>
    /// <param name="registeredDomainId">The domain renewed.</param>
    /// <param name="start">Period start (the current expiry).</param>
    /// <param name="end">Period end.</param>
    /// <exception cref="DomainRuleException">The invoice is no longer editable.</exception>
    public void AddDomainRenewal(string description, decimal amount, int registeredDomainId, DateTimeOffset start, DateTimeOffset end) =>
        AddRenewal(InvoiceItem.CreateRenewal(description, amount, null, registeredDomainId, start, end, null));

    /// <summary>Appends a renewal line under the same editability rule as <see cref="AddItem"/>.</summary>
    /// <param name="item">The renewal line.</param>
    /// <exception cref="DomainRuleException">The invoice is no longer editable.</exception>
    private void AddRenewal(InvoiceItem item)
    {
        EnsureEditable();

        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a line item by ID and recalculates totals. The applied credit may then be more than
    /// what is left to pay; the caller returns <see cref="ExcessCredit"/> to the client's balance
    /// in the same save.
    /// </summary>
    /// <param name="itemId">Id of the line item to remove.</param>
    /// <exception cref="DomainRuleException">
    /// The invoice is no longer editable, has no such line, or would charge less than the payments it already took.
    /// </exception>
    public void RemoveItem(int itemId)
    {
        EnsureEditable();

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainRuleException(BillingErrors.InvoiceItemNotFound, $"Invoice item {itemId} not found.");
        EnsureChargeCoversPayments(SubTotal - item.Amount, TaxRate);
        _items.Remove(item);
        RecalculateTotals();
    }

    /// <summary>
    /// Updates an existing line item and recalculates totals. A lower price may leave the applied
    /// credit above what is left to pay; the caller returns <see cref="ExcessCredit"/> to the
    /// client's balance in the same save.
    /// </summary>
    /// <param name="itemId">Id of the line item to change.</param>
    /// <param name="description">New line text.</param>
    /// <param name="unitPrice">New price of one unit, in the invoice currency.</param>
    /// <param name="quantity">New number of units.</param>
    /// <exception cref="DomainRuleException">
    /// The invoice is no longer editable, has no such line, or would charge less than the payments it already took.
    /// </exception>
    public void UpdateItem(int itemId, string description, decimal unitPrice, int quantity)
    {
        EnsureEditable();

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainRuleException(BillingErrors.InvoiceItemNotFound, $"Invoice item {itemId} not found.");
        EnsureChargeCoversPayments(SubTotal - item.Amount + (unitPrice * quantity), TaxRate);
        item.Update(description, unitPrice, quantity);
        RecalculateTotals();
    }

    /// <summary>
    /// Records a payment against this invoice, full or partial. The invoice turns Paid only once
    /// the recorded payments cover <see cref="Total"/>; until then it stays open and takes further
    /// payments.
    /// </summary>
    /// <param name="date">When the payment was made.</param>
    /// <param name="gateway">Name of the payment gateway or method.</param>
    /// <param name="transactionId">Gateway reference of the payment.</param>
    /// <param name="amount">Amount paid, in the invoice currency.</param>
    /// <param name="fees">Gateway fees charged on the payment.</param>
    /// <param name="notes">Free-form note, or null.</param>
    /// <returns>The client ledger row for this payment, to be saved with the invoice.</returns>
    /// <exception cref="DomainRuleException">
    /// The invoice is not open for payment, or <paramref name="amount"/> is more than <see cref="AmountDue"/>.
    /// A manual overpayment is refused rather than parked; money meant for the balance is added there
    /// directly. A gateway that took more than was due is not refused: its completion settles the
    /// invoice and credits the surplus to the client's balance.
    /// </exception>
    public Transaction AddPayment(DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees, string? notes)
    {
        EnsurePayable();

        if (amount > AmountDue)
        {
            throw new DomainRuleException(
                BillingErrors.InvoicePaymentExceedsDue, $"Payment {amount} is more than the {AmountDue} still owed.");
        }

        var tx = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Payment, fees, notes);
        _transactions.Add(tx);
        if (PaymentsRecorded >= Total)
        {
            ApplyPaid(transactionId, date);
        }

        return Transaction.CreateInvoicePayment(this, date, gateway, transactionId, amount, fees);
    }

    /// <summary>
    /// Moves the invoice's creation and issue dates to an earlier moment, for invoices imported or
    /// seeded from history rather than raised now.
    /// </summary>
    /// <param name="createdAt">The moment the invoice was really raised (UTC).</param>
    public void Backdate(DateTimeOffset createdAt)
    {
        CreatedAt = createdAt;
        InvoiceDate = createdAt;
    }

    /// <summary>
    /// Carries over the tax and credit an invoice held in the billing system it is imported from,
    /// so the imported total is the one the client was billed rather than one recomputed here at
    /// <see cref="TaxRate"/>. The line items are added first; a later edit of the lines recomputes
    /// the tax from <see cref="TaxRate"/> as for any invoice.
    /// </summary>
    /// <param name="tax">Tax the source charged, in the invoice currency.</param>
    /// <param name="credit">Account credit the source applied, in the invoice currency.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tax"/> or <paramref name="credit"/> is negative.</exception>
    /// <exception cref="DomainRuleException">The invoice is not Draft or Unpaid, or already took a payment.</exception>
    public void ImportTotals(decimal tax, decimal credit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tax);
        ArgumentOutOfRangeException.ThrowIfNegative(credit);
        EnsureEditable();
        EnsureNoPayments();

        Tax = CurrencyCode.RoundToMinorUnits(tax, Currency);
        Credit = credit;
        Total = CurrencyCode.RoundToMinorUnits(SubTotal + Tax - Credit, Currency);
    }

    /// <summary>
    /// Updates invoice options (issue date, due date, payment method, tax rate).
    /// </summary>
    /// <param name="invoiceDate">Issue date.</param>
    /// <param name="dueDate">Date payment is due.</param>
    /// <param name="paymentMethod">Gateway module chosen for the invoice, or null.</param>
    /// <param name="taxRate">Tax percentage applied to the subtotal.</param>
    /// <remarks>
    /// A lower rate may leave the applied credit above what is left to pay; the caller returns
    /// <see cref="ExcessCredit"/> to the client's balance in the same save.
    /// </remarks>
    /// <exception cref="DomainRuleException">
    /// The tax rate changes on an invoice that is no longer editable, or would make it charge less
    /// than the payments it already took.
    /// </exception>
    public void UpdateOptions(DateTimeOffset invoiceDate, DateTimeOffset dueDate, string? paymentMethod, decimal taxRate)
    {
        // Dates and the method are bookkeeping and stay editable; the rate moves the total, so it
        // follows the same rule as the lines.
        if (taxRate != TaxRate)
        {
            EnsureEditable();
            EnsureChargeCoversPayments(SubTotal, taxRate);
        }

        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        PaymentMethod = paymentMethod;
        TaxRate = taxRate;
        RecalculateTotals();
    }

    /// <summary>
    /// Applies a credit amount to the invoice, reducing the total owed. Never more than
    /// <see cref="AmountDue"/>, so the total cannot drop below what was already paid.
    /// </summary>
    /// <param name="amount">Credit to apply, in the invoice currency.</param>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is not positive.</exception>
    /// <exception cref="DomainRuleException">
    /// The invoice is not Draft, Unpaid or Overdue, or <paramref name="amount"/> is more than it still owes.
    /// </exception>
    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Credit amount must be positive.", nameof(amount));
        }

        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid or InvoiceStatus.Overdue))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotEditable, $"Cannot apply credit to an invoice with status {Status}.");
        }

        if (amount > AmountDue)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceCreditExceedsDue, $"Credit {amount} is more than the {AmountDue} still owed.");
        }

        Credit += amount;
        RecalculateTotals();
    }

    /// <summary>
    /// Removes a credit amount from the invoice, never more than the credit it holds.
    /// </summary>
    /// <param name="amount">Credit to take back, in the invoice currency.</param>
    /// <returns>The credit actually removed, which the caller returns to the client's balance.</returns>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is not positive.</exception>
    /// <exception cref="DomainRuleException">The invoice is not Draft, Unpaid or Overdue, the same rule as <see cref="ApplyCredit"/>.</exception>
    public decimal RemoveCredit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid or InvoiceStatus.Overdue))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotEditable, $"Cannot remove credit from an invoice with status {Status}.");
        }

        var removed = Math.Min(amount, Credit);
        Credit -= removed;
        RecalculateTotals();
        return removed;
    }

    /// <summary>
    /// Updates or clears the invoice notes.
    /// </summary>
    /// <param name="notes">The new note, or null to clear it.</param>
    public void UpdateNotes(string? notes) => Notes = notes;

    /// <summary>
    /// Marks paid an invoice that owes nothing more (credit or the recorded payments already cover
    /// it), without a ledger row, and raises <see cref="PaymentReceivedEvent"/>. A stored gateway
    /// session is kept (see <see cref="Settle"/>). An invoice that still owes
    /// money is paid through <see cref="Settle"/>, which records the rest and shapes the ledger row,
    /// so a Paid invoice is always paid in full.
    /// </summary>
    /// <param name="gatewayTransactionId">Reference of the payment.</param>
    /// <param name="paidAt">When the payment was received (UTC).</param>
    /// <exception cref="DomainRuleException">The invoice is not Unpaid or Overdue, or <see cref="AmountDue"/> is not zero.</exception>
    public void MarkPaid(string gatewayTransactionId, DateTimeOffset paidAt)
    {
        EnsurePayable();

        if (AmountDue != 0)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceAmountStillDue, $"Invoice still owes {AmountDue}; settle it instead.");
        }

        ApplyPaid(gatewayTransactionId, paidAt);
    }

    /// <summary>
    /// Records an invoice that was paid before it reached this system (imported or seeded history):
    /// it turns Paid without a payment row or ledger row, because the money was taken and booked
    /// where the history comes from. <see cref="AmountPaid"/> then counts the whole
    /// <see cref="Total"/>, which only holds while no payment is recorded on it. Raises
    /// <see cref="PaymentReceivedEvent"/> like any payment.
    /// </summary>
    /// <param name="reference">Reference of the historical payment.</param>
    /// <param name="paidAt">When the history was recorded (UTC).</param>
    /// <exception cref="DomainRuleException">
    /// The invoice is not Unpaid or Overdue, or already took a payment here (it is settled instead).
    /// </exception>
    public void RecordPaidHistory(string reference, DateTimeOffset paidAt)
    {
        EnsurePayable();
        EnsureNoPayments();
        ApplyPaid(reference, paidAt);
    }

    /// <summary>
    /// Marks paid an open invoice whose recorded payments already cover it, under the reference of
    /// its latest payment, and raises <see cref="PaymentReceivedEvent"/>. A credit or a line or tax
    /// change can bring <see cref="AmountDue"/> to zero after a partial payment, and two payments
    /// saved at the same moment each see only themselves; nothing is owed then, so the invoice must
    /// not stay open, go overdue and suspend a client who has paid. Adds no payment and no ledger row.
    /// </summary>
    /// <param name="paidAt">When the invoice became covered (UTC).</param>
    /// <returns>
    /// <see langword="true"/> when the invoice was settled; <see langword="false"/> when it is not open,
    /// has no recorded payment, or still owes money.
    /// </returns>
    public bool SettleIfCovered(DateTimeOffset paidAt)
    {
        var lastPayment = _transactions.LastOrDefault(t => t.Type == InvoiceTransactionType.Payment);
        if (!IsPayable || lastPayment is null || AmountDue > 0)
        {
            return false;
        }

        ApplyPaid(lastPayment.TransactionId, paidAt);
        return true;
    }

    /// <summary>
    /// Settles the invoice with the money still due: marks it paid, raises
    /// <see cref="PaymentReceivedEvent"/> and returns the client ledger row for the amount taken,
    /// so "invoice is Paid" and "ledger row exists" are written in the same save. Every path that
    /// takes the remaining money in one go (gateway completion, a saved card, a bulk mark-paid)
    /// settles here.
    /// </summary>
    /// <param name="paymentMethod">Payment method or gateway name recorded on the ledger row.</param>
    /// <param name="reference">Reference of the payment.</param>
    /// <param name="paidAt">When the payment was received (UTC).</param>
    /// <remarks>
    /// A stored hosted-gateway session is kept whichever path settles the invoice. When the session
    /// took the money, refunds find that gateway through it; when another path paid first, a payment
    /// the session takes later is still matched by the gateway completion, which credits it to the
    /// client's balance instead of losing it. Which gateway took the money is read from the recorded
    /// payments (<see cref="WasPaidThrough"/>), never from the session alone.
    /// </remarks>
    /// <returns>The ledger row, or <see langword="null"/> when nothing was due (a zero total).</returns>
    /// <exception cref="DomainRuleException">The invoice is not Unpaid or Overdue.</exception>
    public Transaction? Settle(string paymentMethod, string reference, DateTimeOffset paidAt)
    {
        EnsurePayable();

        var taken = AmountDue;
        if (taken <= 0)
        {
            ApplyPaid(reference, paidAt);
            return null;
        }

        // The invoice's own payment history must hold every amount it took, so that AmountPaid and
        // RefundableAmount stay right after a partial AddPayment followed by a settlement.
        _transactions.Add(InvoiceTransaction.Create(
            Id, paidAt, paymentMethod, reference, taken, InvoiceTransactionType.Payment, 0m, null));
        ApplyPaid(reference, paidAt);
        return Transaction.CreateInvoicePayment(this, paidAt, paymentMethod, reference, taken, 0m);
    }

    /// <summary>Clears the stored hosted-gateway session fields and the reconciliation bookkeeping of that session.</summary>
    private void ClearGatewaySession()
    {
        GatewayModule = null;
        GatewayOrderId = null;
        GatewayStartedAt = null;
        ResetGatewayChecks();
    }

    /// <summary>Forgets what the reconciler recorded about the previous session, so a new session starts with a clean back-off.</summary>
    private void ResetGatewayChecks()
    {
        GatewayCheckedAt = null;
        GatewayCheckCount = 0;
        GatewayConcludedAt = null;
        GatewayStalledNotifiedAt = null;
    }

    /// <summary>
    /// Records that the reconciler asked the gateway about the latest session: when, one more ask for
    /// the back-off, and, when the gateway gave a final answer that left the invoice unpaid, that the
    /// session is concluded and is not asked about again.
    /// </summary>
    /// <param name="checkedAt">When the gateway was asked, UTC.</param>
    /// <param name="concluded">Whether the gateway's answer was final (declined or expired).</param>
    public void RecordGatewayCheck(DateTimeOffset checkedAt, bool concluded)
    {
        GatewayCheckedAt = checkedAt;
        GatewayCheckCount++;
        if (concluded)
        {
            GatewayConcludedAt = checkedAt;
        }
    }

    /// <summary>Records that staff were told the latest session stayed pending for an unusual time.</summary>
    /// <param name="notifiedAt">When staff were told, UTC.</param>
    public void MarkGatewayStalledNotified(DateTimeOffset notifiedAt)
    {
        GatewayStalledNotifiedAt = notifiedAt;
    }

    /// <summary>
    /// Records a new hosted-gateway payment attempt. Each attempt overwrites the previous
    /// session: the gateway rejects reused order numbers, so retries always re-register.
    /// </summary>
    /// <param name="module">The payment plugin id (e.g. "bank-gateway").</param>
    /// <param name="gatewayOrderId">The gateway-side order id returned at registration.</param>
    /// <param name="startedAt">When the attempt was registered with the gateway, UTC.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not payable.</exception>
    public void SetGatewaySession(string module, string gatewayOrderId, DateTimeOffset startedAt)
    {
        if (!IsPayable)
        {
            throw new InvalidOperationException($"Cannot start a gateway payment for an invoice in status {Status}.");
        }

        GatewayModule = module;
        GatewayOrderId = gatewayOrderId;
        GatewayStartedAt = startedAt;
        ResetGatewayChecks();
    }

    /// <summary>
    /// Transitions the invoice from Unpaid to Overdue and raises <see cref="InvoiceOverdueEvent"/>.
    /// If already Overdue, this is a no-op.
    /// </summary>
    /// <param name="now">The current moment (UTC), stamped on the overdue event.</param>
    public void MarkOverdue(DateTimeOffset now)
    {
        if (Status == InvoiceStatus.Overdue)
        {
            return;
        }

        if (Status != InvoiceStatus.Unpaid)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotUnpaid,
                $"Only Unpaid invoices can be marked overdue; current status is {Status}.");
        }

        Status = InvoiceStatus.Overdue;
        AddDomainEvent(new InvoiceOverdueEvent(Id, ClientId) { OccurredAt = now });
    }

    /// <summary>
    /// Marks an overdue invoice lapsed: it stayed unpaid past its grace period, so the period it billed was
    /// never paid for. Sets <see cref="LapsedAt"/> and raises <see cref="SubscriptionLapsedEvent"/>. Marking an
    /// invoice that has already lapsed is a no-op, so a sweep that runs twice reports it once. The status stays
    /// Overdue: the debt is still owed and can still be paid.
    /// </summary>
    /// <param name="now">The current moment (UTC); becomes <see cref="LapsedAt"/> and is stamped on the event.</param>
    /// <exception cref="DomainRuleException">
    /// <see cref="BillingErrors.InvoiceNotOverdue"/> when the invoice has not lapsed yet and is not Overdue.
    /// </exception>
    public void MarkLapsed(DateTimeOffset now)
    {
        // Checked before the status: an invoice paid after it lapsed is no longer Overdue, and asking again
        // must still answer that it has lapsed rather than refuse a fact that stands.
        if (LapsedAt is not null)
        {
            return;
        }

        if (Status != InvoiceStatus.Overdue)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotOverdue,
                $"Only Overdue invoices can be marked lapsed; current status is {Status}.");
        }

        LapsedAt = now;
        AddDomainEvent(new SubscriptionLapsedEvent(Id, ClientId, HasDigitalItems) { OccurredAt = now });
    }

    /// <summary>
    /// Cancels the invoice so it will not be collected. Applied credit stays on the invoice; the
    /// caller takes it back with <see cref="RemoveCredit"/> first so the client does not lose it.
    /// </summary>
    /// <exception cref="DomainRuleException">See <see cref="EnsureCancellable"/>.</exception>
    public void Cancel()
    {
        EnsureCancellable();
        Status = InvoiceStatus.Cancelled;
    }

    /// <summary>
    /// Refuses a cancel that <see cref="CanCancel"/> does not allow, naming why. A caller that must
    /// return credit before cancelling asks here first, so the refusal comes before anything moves.
    /// </summary>
    /// <exception cref="DomainRuleException">
    /// <c>INVOICE_CANCEL_NOT_ALLOWED</c> when the invoice is paid, refunded or already cancelled;
    /// <c>INVOICE_HAS_PAYMENTS</c> when it is open but a payment is already recorded on it (<see cref="EnsureNoPayments"/>).
    /// </exception>
    public void EnsureCancellable()
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid or InvoiceStatus.Overdue))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceCancelNotAllowed, $"Cannot cancel an invoice with status {Status}.");
        }

        EnsureNoPayments();
    }

    /// <summary>
    /// Reverses a paid invoice back to Unpaid status. Also clears any retained gateway
    /// session: a settled invoice keeps its
    /// <see cref="GatewayModule"/>/<see cref="GatewayOrderId"/>/<see cref="GatewayStartedAt"/>
    /// so refund/reconciliation code can find the paying gateway, but once an admin reverses
    /// that payment there is no longer a live payment to reconcile. Leaving the session behind
    /// would let the reconciler, which keeps asking about pending gateway sessions, see
    /// what still looks like an in-flight payment, re-query the gateway, and silently re-mark
    /// the invoice Paid again, undoing the reversal.
    /// </summary>
    /// <remarks>
    /// Only an invoice closed without payment rows (credit, imported history) can be reopened this
    /// way. Recorded payments would keep <see cref="AmountDue"/> at zero on a reopened invoice; they
    /// are undone by deleting the payment or refunding it, never here.
    /// </remarks>
    /// <exception cref="DomainRuleException">
    /// <c>INVOICE_NOT_PAID</c> when the invoice is not Paid; <c>INVOICE_HAS_PAYMENTS</c> when a payment is recorded on it.
    /// </exception>
    public void MarkUnpaid()
    {
        if (Status != InvoiceStatus.Paid)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotPaid,
                $"Only Paid invoices can be marked Unpaid; current status is {Status}.");
        }

        EnsureNoPayments();

        Status = InvoiceStatus.Unpaid;
        PaidAt = null;
        GatewayTransactionId = null;
        ClearGatewaySession();
    }

    /// <summary>
    /// Records a refund against this paid invoice, full or partial, never more than
    /// <see cref="RefundableAmount"/>. The invoice turns Refunded once the refunds cover everything
    /// it took; a refund that leaves part of the money turns it PartiallyRefunded, and a further
    /// refund of a PartiallyRefunded invoice is held to what is left.
    /// </summary>
    /// <param name="date">When the refund was made.</param>
    /// <param name="gateway">Name of the payment gateway or method.</param>
    /// <param name="transactionId">Gateway reference of the refund.</param>
    /// <param name="amount">Amount refunded, in the invoice currency.</param>
    /// <param name="fees">Gateway fees charged on the refund.</param>
    /// <param name="notes">Free-form note, or null.</param>
    /// <param name="addedToCredit">Whether the refund goes to the client's credit balance instead of back to the payer.</param>
    /// <returns>The client ledger row for this refund, to be saved with the invoice.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amount"/> is not positive.</exception>
    /// <exception cref="DomainRuleException">
    /// The invoice is not Paid or PartiallyRefunded, the amount is more than can still be refunded,
    /// or another refund begun at a different moment is still in flight.
    /// </exception>
    public Transaction AddRefund(
        DateTimeOffset date, string gateway, string transactionId, decimal amount, decimal fees, string? notes, bool addedToCredit = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        EnsureRefundable();

        if (amount > RefundableAmount)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceRefundExceedsPaid, $"Refund {amount} is more than the {RefundableAmount} still refundable.");
        }

        // A refund claimed through BeginRefund records its parts under the claim's own moment; any
        // other refund must wait until that one is answered.
        if (RefundStartedAt != date && IsRefundInFlight(date))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceRefundConflict, "Another refund of this invoice is still waiting for its gateway.");
        }

        // Written on every refund, so the invoice row itself changes and a concurrent refund's save
        // fails the row's concurrency check instead of passing the same RefundableAmount twice.
        RefundStartedAt = date;
        var tx = InvoiceTransaction.Create(Id, date, gateway, transactionId, amount, InvoiceTransactionType.Refund, fees, notes);
        _transactions.Add(tx);
        if (RefundableAmount == 0)
        {
            Refund();
        }
        else
        {
            Status = InvoiceStatus.PartiallyRefunded;
        }

        return Transaction.CreateInvoiceRefund(this, date, gateway, transactionId, amount, addedToCredit);
    }

    /// <summary>
    /// Claims the invoice for a refund that must ask a gateway before it can be recorded. The caller
    /// saves the claim before the gateway call, so of two refunds racing on the same invoice only one
    /// gets to move money; the other fails the save or finds the claim held. The parts the gateway
    /// returns are then recorded with <see cref="AddRefund"/> under the same <paramref name="at"/>, or
    /// the claim is given back with <see cref="ReleaseRefundClaim"/> when it returned nothing.
    /// </summary>
    /// <param name="at">When the refund begins (UTC); also the date its parts are recorded under.</param>
    /// <exception cref="DomainRuleException">The invoice is not Paid or PartiallyRefunded, or another refund is still in flight.</exception>
    public void BeginRefund(DateTimeOffset at)
    {
        EnsureRefundable();

        if (IsRefundInFlight(at))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceRefundConflict, "Another refund of this invoice is still waiting for its gateway.");
        }

        RefundStartedAt = at;
    }

    /// <summary>
    /// Gives back a refund claim the gateway answered without returning any money, so the next
    /// refund need not wait for the lease to run out. A claim that already recorded a part is no
    /// longer in flight and is left as the latest refund's start.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    public void ReleaseRefundClaim(DateTimeOffset now)
    {
        if (IsRefundInFlight(now))
        {
            RefundStartedAt = null;
        }
    }

    /// <summary>
    /// Gets what a pre-tax line amount cost the payer on this invoice: its proportional share of
    /// <see cref="Total"/>, so the invoice's tax is included and its credit (money that never came
    /// from the payer) is left out. Rounded to the currency's minor unit, never more than <see cref="Total"/>.
    /// </summary>
    /// <param name="lineAmount">The line's amount before tax, in the invoice currency.</param>
    /// <returns>The line's share of the total, or zero when the invoice has no subtotal.</returns>
    public decimal ShareOfTotal(decimal lineAmount)
    {
        if (SubTotal <= 0 || lineAmount <= 0)
        {
            return 0m;
        }

        return Math.Min(Total, CurrencyCode.RoundToMinorUnits(Total * lineAmount / SubTotal, Currency));
    }

    /// <summary>
    /// Gets the part of a pre-tax line amount the client paid with credit on this invoice: the line's
    /// share of the total before credit (tax included) less its <see cref="ShareOfTotal"/>, so the two
    /// shares always add up to what the line cost. Never more than <see cref="Credit"/>.
    /// </summary>
    /// <param name="lineAmount">The line's amount before tax, in the invoice currency.</param>
    /// <returns>The line's credit share, or zero when the invoice used no credit or has no subtotal.</returns>
    public decimal ShareOfCredit(decimal lineAmount)
    {
        if (SubTotal <= 0 || lineAmount <= 0 || Credit <= 0)
        {
            return 0m;
        }

        var beforeCredit = SubTotal + Tax;
        var lineCost = Math.Min(beforeCredit, CurrencyCode.RoundToMinorUnits(beforeCredit * lineAmount / SubTotal, Currency));
        return Math.Min(Credit, Math.Max(0m, lineCost - ShareOfTotal(lineAmount)));
    }

    /// <summary>
    /// Creates a new Draft copy of this invoice (with all its line items).
    /// </summary>
    /// <remarks>
    /// The copy carries each line's <see cref="InvoiceItem.ClientServiceId"/> and
    /// <see cref="InvoiceItem.ProductSnapshot"/> across. A duplicate is the same charges billed
    /// again, so a line that was for a service is still for that service and sells what it sold;
    /// dropping the link here would quietly hide the copy from the service it belongs to, and
    /// dropping the snapshot would leave a paid digital copy with nothing to fulfil.
    /// </remarks>
    /// <param name="issuedAt">When the copy is issued (UTC).</param>
    /// <returns>A new Draft invoice with the same client, due date, currency and lines.</returns>
    public Invoice Duplicate(DateTimeOffset issuedAt)
    {
        // The same amounts in the same currency: a copy never re-labels the lines it carries.
        var copy = CreateDraft(ClientId, DueDate, Currency, issuedAt);
        // AddItem, never a renewal factory: a copy is not a renewal, and paying it must extend nothing.
        foreach (var item in _items)
        {
            copy.AddItem(item.Description, item.UnitPrice, item.Quantity, item.ClientServiceId, item.ProductSnapshot);
        }
        return copy;
    }

    /// <summary>
    /// Refunds a paid invoice and marks it as Refunded. <see cref="PaidAt"/> stays: the invoice
    /// was paid at that moment, and Refunded already says the money was given back.
    /// </summary>
    /// <exception cref="DomainRuleException"><c>INVOICE_NOT_PAID</c> when the invoice is not Paid or PartiallyRefunded.</exception>
    public void Refund()
    {
        EnsureRefundable();

        Status = InvoiceStatus.Refunded;
        GatewayTransactionId = null;
    }

    /// <summary>Refuses a refund of an invoice that holds no paid money: anything but Paid or PartiallyRefunded.</summary>
    /// <exception cref="DomainRuleException"><c>INVOICE_NOT_PAID</c> when <see cref="IsPaid"/> is false.</exception>
    private void EnsureRefundable()
    {
        if (!IsPaid)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotPaid, $"Only Paid or PartiallyRefunded invoices can be refunded; current status is {Status}.");
        }
    }

    /// <summary>Refuses any payment while the invoice is not open for one.</summary>
    /// <exception cref="DomainRuleException">The invoice is not Unpaid or Overdue.</exception>
    private void EnsurePayable()
    {
        if (!IsPayable)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotPayable, $"Invoice cannot be paid in status {Status}.");
        }
    }

    /// <summary>
    /// Refuses a cancel, a reopen or a historical close of an invoice that already took a payment:
    /// that money is undone only by deleting the payment or refunding it.
    /// </summary>
    /// <exception cref="DomainRuleException"><c>INVOICE_HAS_PAYMENTS</c> when a payment is recorded on the invoice.</exception>
    private void EnsureNoPayments()
    {
        if (PaymentsRecorded > 0)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceHasPayments, $"The invoice already took {PaymentsRecorded}; refund or delete that payment first.");
        }
    }

    /// <summary>
    /// Refuses a line or tax change whose charge before credit would be less than the payments
    /// already recorded, so <see cref="Total"/> never drops below them once <see cref="ExcessCredit"/> is returned.
    /// </summary>
    /// <param name="subTotal">The sub-total the change would leave.</param>
    /// <param name="taxRate">The tax rate the change would leave.</param>
    /// <exception cref="DomainRuleException"><c>INVOICE_TOTAL_BELOW_PAID</c> when the charge would be less than the payments.</exception>
    private void EnsureChargeCoversPayments(decimal subTotal, decimal taxRate)
    {
        var charge = subTotal + TaxOn(subTotal, taxRate);
        if (charge < PaymentsRecorded)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceTotalBelowPaid, $"The invoice would charge {charge}, less than the {PaymentsRecorded} already paid.");
        }
    }

    /// <summary>Refuses any change to the lines or the tax rate once the invoice has left Draft and Unpaid.</summary>
    /// <exception cref="DomainRuleException">The invoice is not Draft or Unpaid.</exception>
    private void EnsureEditable()
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Unpaid))
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceNotEditable, $"Cannot change the lines of an invoice with status {Status}.");
        }
    }

    /// <summary>
    /// The single place an invoice turns Paid: status, time, reference and event change together. The
    /// stored gateway session stays, so a payment it takes after another path settled the invoice is
    /// still found.
    /// </summary>
    /// <param name="reference">Reference of the payment.</param>
    /// <param name="paidAt">When the payment was received (UTC).</param>
    private void ApplyPaid(string reference, DateTimeOffset paidAt)
    {
        Status = InvoiceStatus.Paid;
        PaidAt = paidAt;
        GatewayTransactionId = reference;
        AddDomainEvent(new PaymentReceivedEvent(Id, ClientId, Total, reference) { OccurredAt = paidAt });
    }

    /// <summary>Sums the recorded transactions of one type.</summary>
    /// <param name="type">Payment or refund.</param>
    /// <returns>The sum, zero when there are none.</returns>
    private decimal SumOf(InvoiceTransactionType type) => _transactions.Where(t => t.Type == type).Sum(t => t.Amount);

    /// <summary>The tax a sub-total carries at a rate, rounded to the invoice currency's minor unit.</summary>
    /// <param name="subTotal">The sub-total, in the invoice currency.</param>
    /// <param name="taxRate">The tax percentage.</param>
    /// <returns>The tax amount, an amount a payer can really be charged.</returns>
    private decimal TaxOn(decimal subTotal, decimal taxRate) =>
        CurrencyCode.RoundToMinorUnits(subTotal * taxRate / 100, Currency);

    /// <summary>
    /// Recomputes <see cref="SubTotal"/>, <see cref="Tax"/>, and <see cref="Total"/> from current
    /// line items, the tax and the total rounded to the currency's minor unit, and
    /// <see cref="HasDigitalItems"/> from the lines' snapshots. Every path that changes a line runs
    /// through here, so the stored flag cannot drift from the lines.
    /// </summary>
    private void RecalculateTotals()
    {
        HasDigitalItems = _items.Any(i => i.IsDigital);
        SubTotal = _items.Sum(i => i.Amount);
        Tax = TaxOn(SubTotal, TaxRate);
        Total = CurrencyCode.RoundToMinorUnits(SubTotal + Tax - Credit, Currency);
    }
}
