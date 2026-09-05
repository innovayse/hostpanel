namespace Innovayse.Domain.Orders;

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Innovayse.Domain.Common;
using Innovayse.Domain.Orders.Events;

/// <summary>
/// Represents a purchase order placed by a client at checkout.
/// An order aggregates one or more <see cref="OrderItem"/> entries and links
/// to an <see cref="InvoiceId"/> for payment tracking.
/// The admin reviews pending orders and accepts, cancels, or marks them as fraud.
/// </summary>
public sealed class Order : AggregateRoot
{
    /// <summary>
    /// Number of random bytes behind a payment token. 32 bytes is the usual floor for a bearer
    /// credential that is guessable in no way other than brute force, and encodes to 43
    /// base64url characters — well inside the column width configured for it.
    /// </summary>
    private const int PaymentTokenBytes = 32;

    /// <summary>Gets the human-readable order number (e.g. "ORD-0001").</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>Gets the FK to the owning client.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public OrderStatus Status { get; private set; }

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
    /// credential — the payer may not have one. They are authorised by knowledge of this token
    /// instead, which is handed to whoever placed the order and to nobody else. Without it the
    /// only thing identifying an order is its primary key, which is sequential and therefore
    /// guessable: anyone could start a gateway session on a stranger's order and, because a
    /// live session locks the invoice for the length of the gateway's session window, keep the
    /// real payer from ever paying.
    /// <para>
    /// Never expose this on a DTO that is read back by anyone other than the payer, and never
    /// log it — it is a bearer credential for that order's payment.
    /// </para>
    /// </remarks>
    public string PaymentToken { get; private set; } = string.Empty;

    /// <summary>Internal mutable list of order items.</summary>
    private readonly List<OrderItem> _items = [];

    /// <summary>Gets the read-only collection of order items.</summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Order() : base(0) { }

    /// <summary>
    /// Creates a new pending order and raises <see cref="OrderCreatedEvent"/>.
    /// </summary>
    /// <param name="orderNumber">Human-readable order number (e.g. "ORD-0001").</param>
    /// <param name="clientId">FK to the client placing the order.</param>
    /// <param name="paymentMethod">Payment gateway module name.</param>
    /// <param name="ipAddress">Client's IP address at checkout.</param>
    /// <returns>A new pending <see cref="Order"/>.</returns>
    public static Order Create(string orderNumber, int clientId, string paymentMethod, string? ipAddress)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            ClientId = clientId,
            PaymentMethod = paymentMethod,
            IpAddress = ipAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            PaymentToken = NewPaymentToken(),
        };
        order.AddDomainEvent(new OrderCreatedEvent(0, clientId));
        return order;
    }

    /// <summary>
    /// Adds a line item to this order with snapshotted product data.
    /// </summary>
    /// <param name="productId">FK to the product.</param>
    /// <param name="productName">Product name at order time.</param>
    /// <param name="billingCycle">Billing cycle: "monthly" or "annual".</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="domain">Optional domain name for hosting products.</param>
    /// <param name="hostname">Optional hostname for VPS/server products.</param>
    /// <param name="domainAction">Domain action: "register" or "transfer". Null for hosting.</param>
    /// <param name="eppCode">EPP code for domain transfers.</param>
    /// <param name="years">Domain registration period in years.</param>
    public void AddItem(
        int productId,
        string productName,
        string billingCycle,
        decimal firstPaymentAmount,
        decimal recurringAmount,
        string? domain,
        string? hostname,
        string? domainAction = null,
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
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void Accept()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot accept an order with status {Status}. Only Pending orders can be accepted.");
        }

        Status = OrderStatus.Active;

        foreach (var item in _items)
        {
            item.MarkActive();
        }

        AddDomainEvent(new OrderAcceptedEvent(Id, ClientId));
    }

    /// <summary>
    /// Cancels the order, transitioning from Pending to Cancelled.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void Cancel()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel an order with status {Status}. Only Pending orders can be cancelled.");
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
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Pending status.</exception>
    public void MarkFraud()
    {
        if (Status is not OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot mark an order as fraud with status {Status}. Only Pending orders can be flagged.");
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
    /// characters differ, and the time it took is visible to whoever sent the guess — enough,
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

    /// <summary>
    /// Generates a fresh payment token.
    /// </summary>
    /// <returns>A URL-safe, base64url-encoded random token of <see cref="PaymentTokenBytes"/> bytes.</returns>
    private static string NewPaymentToken()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(PaymentTokenBytes));
}
