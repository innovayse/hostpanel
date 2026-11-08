using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Orders.Events;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Orders;

/// <summary>
/// Represents a purchase order placed by a client at checkout.
/// An order aggregates one or more <see cref="OrderItem"/> entries and links
/// to an <see cref="InvoiceId"/> for payment tracking.
/// The admin reviews pending orders and accepts, cancels, or marks them as fraud.
/// </summary>
public sealed class Order : BaseEntity, IAggregateRoot
{
    /// <summary>Longest order number the order number column holds.</summary>
    public const int OrderNumberMaxLength = 20;

    /// <summary>
    /// Number of random bytes behind a payment token. 32 bytes is the usual floor for a bearer
    /// credential that is guessable in no way other than brute force, and encodes to 43
    /// base64url characters: well inside the column width configured for it.
    /// </summary>
    private const int PaymentTokenBytes = 32;

    /// <summary>Gets the human-readable order number (e.g. "ORD-0001").</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Gets the ISO 4217 alpha code the order's amounts are in. Set at creation, never changed.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the payment gateway module name selected at checkout.</summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the linked invoice, or null if not yet created.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the client's IP address at checkout time.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the admin notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Gets the UTC timestamp when the order was placed.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Gets the capability token that authorises paying for this order.
    /// </summary>
    /// <remarks>
    /// Checkout is open to guests, so the payment endpoints for an order cannot require a
    /// credential: the payer may not have one. They are authorised by knowledge of this token
    /// instead, which is handed to whoever placed the order and to nobody else. Without it the
    /// only thing identifying an order is its primary key, which is sequential and therefore
    /// guessable: anyone could start a gateway session on a stranger's order and, because a
    /// live session locks the invoice for the length of the gateway's session window, keep the
    /// real payer from ever paying.
    /// <para>
    /// Never expose this on a DTO that is read back by anyone other than the payer, and never
    /// log it: it is a bearer credential for that order's payment.
    /// </para>
    /// </remarks>
    public string PaymentToken { get; private set; } = string.Empty;

    /// <summary>Internal mutable list of order items.</summary>
    private readonly List<OrderItem> _items = [];

    /// <summary>Gets the read-only collection of order items.</summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets whether the order may be deleted: only one that never went anywhere (Pending) or was
    /// called off (Cancelled). An accepted or fraud-flagged order is the record of what happened.
    /// </summary>
    public bool CanBeDeleted => Status is OrderStatus.Pending or OrderStatus.Cancelled;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Order() { }

    /// <summary>
    /// Creates a new pending order and raises <see cref="OrderCreatedEvent"/>.
    /// </summary>
    /// <param name="orderNumber">Human-readable order number (e.g. "ORD-0001").</param>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="paymentMethod">Payment gateway module name.</param>
    /// <param name="ipAddress">Client's IP address at checkout.</param>
    /// <param name="currency">ISO 4217 alpha code the order's amounts are in (the client's billing currency).</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <returns>A new pending <see cref="Order"/>.</returns>
    /// <exception cref="ArgumentException">The currency is not a three-letter code.</exception>
    public static Order Create(string orderNumber, int clientId, string paymentMethod, string? ipAddress, string currency, DateTimeOffset now)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            ClientId = clientId,
            Currency = CurrencyCode.Normalise(currency),
            PaymentMethod = paymentMethod,
            IpAddress = ipAddress,
            Status = OrderStatus.Pending,
            PaymentToken = NewPaymentToken(),
        };
        order.AddDomainEvent(new OrderCreatedEvent(0, clientId, now));
        return order;
    }

    /// <summary>Moves the placement date to an earlier moment, for orders imported or seeded from history.</summary>
    /// <param name="createdAt">The moment the order was really placed (UTC).</param>
    public void Backdate(DateTimeOffset createdAt) => CreatedAt = createdAt;

    /// <summary>
    /// Adds a line item to this order with snapshotted product data.
    /// </summary>
    /// <param name="productId">FK to the product.</param>
    /// <param name="productName">Product name at order time.</param>
    /// <param name="billingCycle">The billing cycle.</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="domain">Optional domain name for hosting products.</param>
    /// <param name="hostname">Optional hostname for VPS/server products.</param>
    /// <param name="domainAction">The domain action. Null for hosting.</param>
    /// <param name="eppCode">EPP code for domain transfers.</param>
    /// <param name="years">Domain registration period in years.</param>
    public void AddItem(
        int productId,
        string productName,
        BillingCycle billingCycle,
        decimal firstPaymentAmount,
        decimal recurringAmount,
        string? domain,
        string? hostname,
        DomainAction? domainAction = null,
        string? eppCode = null,
        int? years = null)
    {
        _items.Add(OrderItem.Create(
            productId, productName, billingCycle,
            firstPaymentAmount, recurringAmount,
            domain, hostname, domainAction, eppCode, years));
    }

    /// <summary>
    /// Accepts the order, transitioning from Pending to Active.
    /// Raises <see cref="OrderAcceptedEvent"/>.
    /// </summary>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <exception cref="DomainRuleException">Thrown when the order is not in Pending status.</exception>
    public void Accept(DateTimeOffset now)
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderNotPending,
                $"Cannot accept an order with status {Status}. Only Pending orders can be accepted.");
        }

        Status = OrderStatus.Active;

        foreach (var item in _items)
        {
            item.MarkActive();
        }

        AddDomainEvent(new OrderAcceptedEvent(Id, ClientId, now));
    }

    /// <summary>
    /// Tells whether a retry may claim the line now: the order was accepted, and the line failed (or a
    /// retry's claim on it has run out).
    /// </summary>
    /// <param name="itemId">The order line.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="lease">How long a retry's claim on a line is respected before another retry may take the line over.</param>
    /// <returns><see langword="false"/> for a line this order does not hold, or one that may not be retried.</returns>
    public bool CanRetryLine(int itemId, DateTimeOffset now, TimeSpan lease)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        return item is not null && Status is OrderStatus.Incomplete && item.CanBeRetried(now, lease);
    }

    /// <summary>
    /// Claims a failed line for a retry: the line is in progress, its attempt is counted, and the order
    /// stays <see cref="OrderStatus.Incomplete"/> until the outcome is recorded.
    /// </summary>
    /// <param name="itemId">The order line to retry.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="lease">How long a retry's claim on a line is respected before another retry may take the line over.</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line or the line may not be retried now.</exception>
    public void StartLineRetry(int itemId, DateTimeOffset now, TimeSpan lease)
    {
        if (!CanRetryLine(itemId, now, lease))
        {
            throw new DomainRuleException(
                OrdersErrors.OrderLineNotRetryable,
                $"Line {itemId} of order {Id} cannot be retried: only a failed line of an accepted order can.");
        }

        _items.First(i => i.Id == itemId).BeginAttempt(now);
    }

    /// <summary>
    /// Claims every line a run over the order has still to do, before any work starts: a line that never ran or whose last
    /// attempt failed is in progress, its attempt is counted, and the order is <see cref="OrderStatus.Incomplete"/> until the
    /// outcomes are recorded. A run that dies part way therefore leaves its lines claimed, which a retry takes over once the
    /// claim runs out, instead of lines that read as never started on an order that reads as complete. A line already
    /// claimed, fulfilled or given back is left alone, and the status of an order that was not accepted (pending, cancelled,
    /// fraud) is the admin's decision and stays as it is.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="onlyItemId">The one line to claim, or <see langword="null"/> for every line that is pending or failed.</param>
    public void BeginFulfilment(DateTimeOffset now, int? onlyItemId = null)
    {
        foreach (var item in _items.Where(i => (onlyItemId is null || i.Id == onlyItemId) && i.IsUnclaimed()))
        {
            item.BeginAttempt(now);
        }

        RefreshStatusFromLines();
    }

    /// <summary>
    /// Marks every line that never ran as fulfilled, for an order imported or seeded from history: its source delivered
    /// the lines long ago, so they must not read as forgotten.
    /// </summary>
    /// <param name="now">The moment the history says the lines were fulfilled (UTC).</param>
    public void MarkLinesFulfilledFromHistory(DateTimeOffset now)
    {
        foreach (var item in _items.Where(i => i.FulfilmentState is OrderItemFulfilmentState.Pending))
        {
            item.MarkFulfilledFromHistory(now);
        }

        RefreshStatusFromLines();
    }

    /// <summary>
    /// Records that an attempt fulfilled the line, and brings the order's status in line with its lines.
    /// </summary>
    /// <param name="itemId">The order line.</param>
    /// <param name="attempt">The attempt number the run was given when its claim was saved; a late outcome of an older attempt is refused.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line, or the attempt no longer holds it.</exception>
    public void RecordLineFulfilled(int itemId, int attempt, DateTimeOffset now)
    {
        FindItem(itemId).RecordFulfilled(attempt, now);
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Records that an attempt failed the line, and marks the order <see cref="OrderStatus.Incomplete"/> when it was accepted.
    /// </summary>
    /// <param name="itemId">The order line.</param>
    /// <param name="attempt">The attempt number the run was given when its claim was saved; a late outcome of an older attempt is refused.</param>
    /// <param name="errorCode">The refusing use case's code, or <see langword="null"/> when it gave none.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line, or the attempt no longer holds it.</exception>
    public void RecordLineFailed(int itemId, int attempt, string? errorCode, DateTimeOffset now)
    {
        FindItem(itemId).RecordFailed(attempt, errorCode, now);
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Tells whether an attempt may still record its outcome on the line: the line is still in progress under that very
    /// attempt, so neither a refund nor a newer attempt that took the line over has overtaken it.
    /// </summary>
    /// <param name="itemId">The order line.</param>
    /// <param name="attempt">The attempt number the run was given when its claim was saved.</param>
    /// <returns><see langword="false"/> for a line this order does not hold, or one the attempt no longer holds.</returns>
    public bool CanRecordLineOutcome(int itemId, int attempt)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)?.HoldsAttempt(attempt) == true;
    }

    /// <summary>Tells whether a refund of the line is under way, which is when its settlement may be recorded.</summary>
    /// <param name="itemId">The order line.</param>
    /// <returns><see langword="false"/> for a line this order does not hold, or one that is not being refunded.</returns>
    public bool IsLineBeingRefunded(int itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)?.IsBeingRefunded == true;
    }

    /// <summary>
    /// Tells whether a failed line may start being refunded: it failed, and no retry holds it.
    /// </summary>
    /// <param name="itemId">The order line.</param>
    /// <returns><see langword="false"/> for a line this order does not hold, or one that is not failed.</returns>
    public bool CanRefundLine(int itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)?.IsFailed == true;
    }

    /// <summary>
    /// Claims a failed line for its refund before any money moves. A retry cannot take the line while it is being
    /// refunded, so the line is never both delivered and refunded; the order stays incomplete.
    /// </summary>
    /// <param name="itemId">The failed order line whose cost is about to be given back.</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line or the line is not failed.</exception>
    public void BeginLineRefund(int itemId)
    {
        FindItem(itemId).BeginRefund();
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Records that a line's whole cost was given back. The line is closed for good, so no retry can
    /// deliver what was refunded, and it no longer keeps the order incomplete.
    /// </summary>
    /// <param name="itemId">The order line that was refunded.</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line or the line is not being refunded.</exception>
    public void RecordLineRefunded(int itemId)
    {
        FindItem(itemId).RecordRefunded();
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Records that a refund ended without giving back the whole cost: a refund that returned nothing puts the
    /// line back to failed so it can be retried, and one that returned only part leaves the line for staff to
    /// finish by hand. Either way the order stays incomplete.
    /// </summary>
    /// <param name="itemId">The order line whose refund ended.</param>
    /// <param name="partlyRefunded">Whether some of the cost was already given back.</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line or the line is not being refunded.</exception>
    public void RecordLineRefundReleased(int itemId, bool partlyRefunded)
    {
        FindItem(itemId).ReleaseRefund(partlyRefunded);
        RefreshStatusFromLines();
    }

    /// <summary>Tells whether staff can close the line as refunded: its refund stopped part way and they finished it by hand.</summary>
    /// <param name="itemId">The order line.</param>
    /// <returns><see langword="false"/> for a line this order does not hold, or one that is not partly refunded.</returns>
    public bool CanCloseLineRefund(int itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)?.CanCloseRefund == true;
    }

    /// <summary>
    /// Records that staff finished a partly done refund by hand: the line is refunded for good, and the order is
    /// complete again unless another line is still open.
    /// </summary>
    /// <param name="itemId">The partly refunded order line.</param>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line or the line is not partly refunded.</exception>
    public void CloseLineRefund(int itemId)
    {
        FindItem(itemId).CloseRefund();
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Brings the status in line with the lines as they are now. Several lines can finish at the same moment, each
    /// run looking at the others as they were when it began, so the status a run computed from its own copy may
    /// be out of date; the caller loads the order afresh and asks this, and the last line to finish settles it.
    /// </summary>
    public void RefreshStatus()
    {
        RefreshStatusFromLines();
    }

    /// <summary>
    /// Cancels the order, transitioning from Pending to Cancelled.
    /// </summary>
    /// <exception cref="DomainRuleException">Thrown when the order is not in Pending status.</exception>
    public void Cancel()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderNotPending,
                $"Cannot cancel an order with status {Status}. Only Pending orders can be cancelled.");
        }

        Status = OrderStatus.Cancelled;

        foreach (var item in _items)
        {
            item.MarkCancelled();
        }
    }

    /// <summary>
    /// Marks the order as fraudulent.
    /// </summary>
    /// <exception cref="DomainRuleException">Thrown when the order is not in Pending status.</exception>
    public void MarkFraud()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderNotPending,
                $"Cannot mark an order as fraud with status {Status}. Only Pending orders can be flagged.");
        }

        Status = OrderStatus.Fraud;

        foreach (var item in _items)
        {
            item.MarkCancelled();
        }
    }

    /// <summary>
    /// Links this order to an invoice for payment tracking.
    /// </summary>
    /// <param name="invoiceId">FK to the invoice.</param>
    public void LinkInvoice(int invoiceId)
    {
        InvoiceId = invoiceId;
    }

    /// <summary>
    /// Sets or clears the admin notes.
    /// </summary>
    /// <param name="notes">Admin notes text, or null to clear.</param>
    public void SetNotes(string? notes)
    {
        Notes = notes;
    }

    /// <summary>
    /// Tells whether <paramref name="token"/> is this order's payment token.
    /// </summary>
    /// <remarks>
    /// The comparison is fixed-time. An ordinary string comparison returns as soon as two
    /// characters differ, and the time it took is visible to whoever sent the guess: enough,
    /// over many attempts, to recover the token one character at a time. Length is compared
    /// first because a fixed-time comparison is only defined over equal-length inputs; the
    /// length of a token is not a secret, its contents are.
    /// </remarks>
    /// <param name="token">The token presented by the caller, or null when none was sent.</param>
    /// <returns><see langword="true"/> when the token matches; otherwise <see langword="false"/>.</returns>
    public bool MatchesPaymentToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(PaymentToken))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(token);
        var expected = Encoding.UTF8.GetBytes(PaymentToken);

        return presented.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(presented, expected);
    }

    /// <summary>Finds one of this order's lines.</summary>
    /// <param name="itemId">The order line.</param>
    /// <returns>The line.</returns>
    /// <exception cref="DomainRuleException">Thrown when the order holds no such line.</exception>
    private OrderItem FindItem(int itemId)
    {
        return _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainRuleException(
                OrdersErrors.OrderItemNotFound,
                $"Order {Id} has no line {itemId}.");
    }

    /// <summary>
    /// Makes the status say what the lines say: an accepted order with a failed, running-retry or being-refunded line
    /// (or one whose refund is only part done) is <see cref="OrderStatus.Incomplete"/>, and one with none is
    /// <see cref="OrderStatus.Active"/>. Any other
    /// status (pending, cancelled, fraud) is the admin's decision about the order and is left alone.
    /// </summary>
    private void RefreshStatusFromLines()
    {
        if (Status is not (OrderStatus.Active or OrderStatus.Incomplete))
        {
            return;
        }

        var open = _items.Any(i => i.KeepsOrderIncomplete);
        Status = open ? OrderStatus.Incomplete : OrderStatus.Active;
    }

    /// <summary>
    /// Generates a fresh payment token.
    /// </summary>
    /// <returns>A URL-safe, base64url-encoded random token of <see cref="PaymentTokenBytes"/> bytes.</returns>
    private static string NewPaymentToken()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(PaymentTokenBytes));
}
