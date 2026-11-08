using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Orders;

/// <summary>
/// Represents a single line item within an <see cref="Order"/>.
/// Snapshots the product name and price at order time so price changes
/// after ordering do not affect existing orders.
/// </summary>
public sealed class OrderItem : BaseEntity
{
    /// <summary>Longest error code the fulfilment error code column holds.</summary>
    public const int FulfilmentErrorCodeMaxLength = 100;

    /// <summary>
    /// How long a retry's claim on a line is respected. A retry runs one registrar or provisioning call, so a
    /// claim older than this belongs to a run that died, and the line may be claimed again.
    /// </summary>
    private static readonly TimeSpan RetryLease = TimeSpan.FromMinutes(10);

    /// <summary>Gets the FK to the parent order.</summary>
    public int OrderId { get; private set; }

    /// <summary>Gets the FK to the ordered product.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets the product name snapshot at order time.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Gets the selected billing cycle.</summary>
    public BillingCycle BillingCycle { get; private set; }

    /// <summary>Gets the optional domain name for hosting products.</summary>
    public string? Domain { get; private set; }

    /// <summary>Gets the optional hostname for VPS/server products.</summary>
    public string? Hostname { get; private set; }

    /// <summary>Gets the domain action this line performs. Null for hosting items.</summary>
    public DomainAction? DomainAction { get; private set; }

    /// <summary>Gets the EPP/authorization code for domain transfers. Null for registrations.</summary>
    public string? EppCode { get; private set; }

    /// <summary>Gets the domain registration or transfer period in years. Null for hosting items.</summary>
    public int? Years { get; private set; }

    /// <summary>Gets the first payment amount snapshot at order time.</summary>
    public decimal FirstPaymentAmount { get; private set; }

    /// <summary>Gets the recurring charge amount snapshot at order time.</summary>
    public decimal RecurringAmount { get; private set; }

    /// <summary>Gets the current lifecycle status of this item.</summary>
    public OrderItemStatus Status { get; private set; }

    /// <summary>Gets where fulfilling this line stands: what its acceptance or the last retry made of it.</summary>
    public OrderItemFulfilmentState FulfilmentState { get; private set; }

    /// <summary>
    /// Gets the code the last failed attempt ended with, or <see langword="null"/> when the line did not
    /// fail or the refusing use case gave none. Cleared when a later attempt succeeds.
    /// </summary>
    public string? FulfilmentErrorCode { get; private set; }

    /// <summary>Gets how many times fulfilling this line has been attempted, the first run included.</summary>
    public int FulfilmentAttempts { get; private set; }

    /// <summary>Gets when the last attempt started or ended, or <see langword="null"/> before the first one.</summary>
    public DateTimeOffset? LastFulfilmentAttemptAt { get; private set; }

    /// <summary>Gets whether the line failed and no money was given back for it, so staff may retry it.</summary>
    public bool IsFailed => FulfilmentState is OrderItemFulfilmentState.Failed;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private OrderItem() { }

    /// <summary>
    /// Creates a new pending order item with snapshotted product data and optional domain registration fields.
    /// </summary>
    /// <param name="productId">FK to the product.</param>
    /// <param name="productName">Product name at order time.</param>
    /// <param name="billingCycle">The billing cycle.</param>
    /// <param name="firstPaymentAmount">First payment amount.</param>
    /// <param name="recurringAmount">Recurring charge amount.</param>
    /// <param name="domain">Optional domain name.</param>
    /// <param name="hostname">Optional hostname.</param>
    /// <param name="domainAction">The domain action. Null for hosting.</param>
    /// <param name="eppCode">EPP authorization code for transfers.</param>
    /// <param name="years">Registration period in years for domain items.</param>
    /// <returns>A new pending <see cref="OrderItem"/>.</returns>
    internal static OrderItem Create(
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
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            BillingCycle = billingCycle,
            FirstPaymentAmount = firstPaymentAmount,
            RecurringAmount = recurringAmount,
            Domain = domain,
            Hostname = hostname,
            DomainAction = domainAction,
            EppCode = eppCode,
            Years = years,
            Status = OrderItemStatus.Pending,
        };
    }

    /// <summary>Marks this item as fulfilled.</summary>
    internal void MarkActive() => Status = OrderItemStatus.Active;

    /// <summary>
    /// Tells whether a retry may claim this line now: it failed, or a retry claimed it so long ago that
    /// the claim is taken for a run that died before recording its outcome.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns><see langword="true"/> when the line may be claimed for a retry.</returns>
    internal bool CanBeRetried(DateTimeOffset now)
    {
        return FulfilmentState switch
        {
            OrderItemFulfilmentState.Failed => true,
            OrderItemFulfilmentState.InProgress => LastFulfilmentAttemptAt is { } startedAt && startedAt + RetryLease <= now,
            _ => false,
        };
    }

    /// <summary>Claims the line for a retry: it is in progress and the attempt is counted before any work starts.</summary>
    /// <param name="now">The current moment (UTC).</param>
    internal void BeginRetry(DateTimeOffset now)
    {
        FulfilmentState = OrderItemFulfilmentState.InProgress;
        FulfilmentAttempts++;
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>Records that the line was fulfilled; an attempt that no retry had already counted is counted here.</summary>
    /// <param name="now">The current moment (UTC).</param>
    internal void RecordFulfilled(DateTimeOffset now)
    {
        CountAttemptIfUnclaimed();
        FulfilmentState = OrderItemFulfilmentState.Fulfilled;
        FulfilmentErrorCode = null;
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>Records that the line failed; an attempt that no retry had already counted is counted here.</summary>
    /// <param name="errorCode">The refusing use case's code, or <see langword="null"/> when it gave none.</param>
    /// <param name="now">The current moment (UTC).</param>
    internal void RecordFailed(string? errorCode, DateTimeOffset now)
    {
        CountAttemptIfUnclaimed();
        FulfilmentState = OrderItemFulfilmentState.Failed;
        FulfilmentErrorCode = errorCode is null ? null : Truncate(errorCode);
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>Closes a failed line whose cost was given back, so no retry can fulfil what was refunded.</summary>
    internal void RecordRefunded()
    {
        FulfilmentState = OrderItemFulfilmentState.Refunded;
    }

    /// <summary>Counts an attempt unless a retry already counted it when it claimed the line.</summary>
    private void CountAttemptIfUnclaimed()
    {
        if (FulfilmentState is not OrderItemFulfilmentState.InProgress)
        {
            FulfilmentAttempts++;
        }
    }

    /// <summary>Cuts a code to the width its column holds, so an odd code can never fail the save that records it.</summary>
    /// <param name="errorCode">The code to store.</param>
    /// <returns>The code, at most <see cref="FulfilmentErrorCodeMaxLength"/> characters long.</returns>
    private static string Truncate(string errorCode)
    {
        return errorCode.Length <= FulfilmentErrorCodeMaxLength ? errorCode : errorCode[..FulfilmentErrorCodeMaxLength];
    }

    /// <summary>Marks this item as cancelled.</summary>
    internal void MarkCancelled() => Status = OrderItemStatus.Cancelled;
}
