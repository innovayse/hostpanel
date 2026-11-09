using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

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

    /// <summary>
    /// Gets whether the line still keeps its order from being complete: it failed, a retry is running it, or its
    /// refund is under way or only partly done. Refunded, fulfilled and never-run lines do not.
    /// </summary>
    public bool KeepsOrderIncomplete => FulfilmentState is OrderItemFulfilmentState.Failed
        or OrderItemFulfilmentState.InProgress
        or OrderItemFulfilmentState.Refunding
        or OrderItemFulfilmentState.PartiallyRefunded;

    /// <summary>
    /// Gets whether the line can be closed as refunded by hand: only a line whose refund stopped part way
    /// (<see cref="OrderItemFulfilmentState.PartiallyRefunded"/>) waits for staff to finish it.
    /// </summary>
    public bool CanCloseRefund => FulfilmentState is OrderItemFulfilmentState.PartiallyRefunded;

    /// <summary>Gets whether a refund of the line is under way, so only its settlement may change the line.</summary>
    public bool IsBeingRefunded => FulfilmentState is OrderItemFulfilmentState.Refunding;

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
    /// <param name="lease">
    /// How long a retry's claim is respected. A retry runs one registrar or provisioning call, so a claim older than
    /// this belongs to a run that died, and the line may be claimed again.
    /// </param>
    /// <returns><see langword="true"/> when the line may be claimed for a retry.</returns>
    internal bool CanBeRetried(DateTimeOffset now, TimeSpan lease)
    {
        return FulfilmentState switch
        {
            OrderItemFulfilmentState.Failed => true,
            OrderItemFulfilmentState.InProgress => LastFulfilmentAttemptAt is { } startedAt && startedAt + lease <= now,
            _ => false,
        };
    }

    /// <summary>
    /// Claims the line for an attempt, a retry's or a run over the whole order: it is in progress and the attempt is
    /// counted before any work starts, so the attempt's number is the one its outcome must carry.
    /// </summary>
    /// <param name="now">The current moment (UTC).</param>
    internal void BeginAttempt(DateTimeOffset now)
    {
        FulfilmentState = OrderItemFulfilmentState.InProgress;
        FulfilmentAttempts++;
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>
    /// Tells whether a run over the whole order claims this line: it never ran, or its last attempt failed. A line a
    /// claim already holds (in progress), a fulfilled one and one given back are left as they are.
    /// </summary>
    /// <returns><see langword="true"/> when the line is pending or failed.</returns>
    internal bool IsUnclaimed()
    {
        return FulfilmentState is OrderItemFulfilmentState.Pending or OrderItemFulfilmentState.Failed;
    }

    /// <summary>
    /// Tells whether an outcome belongs to the attempt that holds the line now: the line is in progress, and the attempt
    /// is the one counted when the line was claimed. An attempt whose claim ran out and was taken over by a newer one
    /// carries a smaller number, so its late outcome does not match.
    /// </summary>
    /// <param name="attempt">The attempt number the run was given when its claim was saved.</param>
    /// <returns><see langword="true"/> when the outcome may be recorded.</returns>
    internal bool HoldsAttempt(int attempt)
    {
        return FulfilmentState is OrderItemFulfilmentState.InProgress && attempt == FulfilmentAttempts;
    }

    /// <summary>Records that the attempt fulfilled the line.</summary>
    /// <param name="attempt">The attempt number the run was given when its claim was saved.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="DomainRuleException">Thrown when the attempt no longer holds the line: it was refunded, or a newer attempt took it over.</exception>
    internal void RecordFulfilled(int attempt, DateTimeOffset now)
    {
        EnsureOutcomeCanBeRecorded(attempt);
        FulfilmentState = OrderItemFulfilmentState.Fulfilled;
        FulfilmentErrorCode = null;
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>Records that the attempt failed the line.</summary>
    /// <param name="attempt">The attempt number the run was given when its claim was saved.</param>
    /// <param name="errorCode">The refusing use case's code, or <see langword="null"/> when it gave none.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="DomainRuleException">Thrown when the attempt no longer holds the line: it was refunded, or a newer attempt took it over.</exception>
    internal void RecordFailed(int attempt, string? errorCode, DateTimeOffset now)
    {
        EnsureOutcomeCanBeRecorded(attempt);
        FulfilmentState = OrderItemFulfilmentState.Failed;
        FulfilmentErrorCode = errorCode is null ? null : Truncate(errorCode);
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>
    /// Claims a failed line for its refund: no retry can take it while the money goes back, so what is refunded is
    /// never also delivered.
    /// </summary>
    /// <exception cref="DomainRuleException">Thrown when the line did not fail, so there is nothing to give back.</exception>
    internal void BeginRefund()
    {
        if (FulfilmentState is not OrderItemFulfilmentState.Failed)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderLineNotRefundable,
                $"Order line {Id} is {FulfilmentState}; only a failed line can start being refunded.");
        }

        FulfilmentState = OrderItemFulfilmentState.Refunding;
    }

    /// <summary>Closes a line whose refund was completed, so no retry can fulfil what was refunded.</summary>
    /// <exception cref="DomainRuleException">Thrown when the line is not being refunded.</exception>
    internal void RecordRefunded()
    {
        EnsureRefunding();
        FulfilmentState = OrderItemFulfilmentState.Refunded;
    }

    /// <summary>
    /// Settles a refund that did not complete: a refund that returned nothing puts the line back to
    /// <see cref="OrderItemFulfilmentState.Failed"/>, one that returned only part of the cost leaves it
    /// <see cref="OrderItemFulfilmentState.PartiallyRefunded"/> for staff to finish by hand.
    /// </summary>
    /// <param name="partlyRefunded">Whether some of the cost was already given back.</param>
    /// <exception cref="DomainRuleException">Thrown when the line is not being refunded.</exception>
    internal void ReleaseRefund(bool partlyRefunded)
    {
        EnsureRefunding();
        FulfilmentState = partlyRefunded ? OrderItemFulfilmentState.PartiallyRefunded : OrderItemFulfilmentState.Failed;
    }

    /// <summary>
    /// Closes a line whose refund staff finished by hand: the part of the cost the automatic refund could not give back
    /// is back now, so the line is refunded for good and no longer keeps its order incomplete.
    /// </summary>
    /// <exception cref="DomainRuleException">Thrown when the line is not partly refunded.</exception>
    internal void CloseRefund()
    {
        if (!CanCloseRefund)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderLineNotRefundable,
                $"Order line {Id} is {FulfilmentState}; only a partly refunded line can be closed as refunded.");
        }

        FulfilmentState = OrderItemFulfilmentState.Refunded;
    }

    /// <summary>
    /// Marks a line of an order imported or seeded from history as fulfilled: its source delivered it long ago, so
    /// it is one finished attempt.
    /// </summary>
    /// <param name="now">The moment the history says it was fulfilled (UTC).</param>
    internal void MarkFulfilledFromHistory(DateTimeOffset now)
    {
        FulfilmentState = OrderItemFulfilmentState.Fulfilled;
        FulfilmentAttempts = 1;
        LastFulfilmentAttemptAt = now;
    }

    /// <summary>Refuses a settlement of a refund that was never begun.</summary>
    /// <exception cref="DomainRuleException">Thrown when the line is not being refunded.</exception>
    private void EnsureRefunding()
    {
        if (FulfilmentState is not OrderItemFulfilmentState.Refunding)
        {
            throw new DomainRuleException(
                OrdersErrors.OrderLineNotRefundable,
                $"Order line {Id} is {FulfilmentState}; only a line being refunded can be settled.");
        }
    }

    /// <summary>
    /// Refuses an outcome that is not the holding attempt's: a late outcome of an attempt whose claim ran out and was
    /// taken over, or one the refund overtook, must not overwrite what the newer attempt or the refund made of the line.
    /// </summary>
    /// <param name="attempt">The attempt number the outcome belongs to.</param>
    /// <exception cref="DomainRuleException">Thrown when the line is not in progress under that attempt.</exception>
    private void EnsureOutcomeCanBeRecorded(int attempt)
    {
        if (!HoldsAttempt(attempt))
        {
            throw new DomainRuleException(
                OrdersErrors.OrderLineChanged,
                $"Order line {Id} is {FulfilmentState} at attempt {FulfilmentAttempts}; the outcome of attempt {attempt} cannot be recorded on it.");
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
