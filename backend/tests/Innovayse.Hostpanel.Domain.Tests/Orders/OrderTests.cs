using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Orders.Events;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Orders;

/// <summary>Unit tests for the <see cref="Order"/> aggregate's status invariant.</summary>
public sealed class OrderTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The retry lease every retry in these tests is claimed under.</summary>
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    /// <summary>The currency given at creation is what the order reports, upper-cased.</summary>
    [Fact]
    public void Create_StampsTheCurrency()
    {
        var order = Order.Create("ORD-0001", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "eur", now: Now);

        order.Currency.Should().Be("EUR");
    }

    /// <summary>An order without a three-letter currency is refused.</summary>
    /// <param name="currency">A missing or malformed code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("US")]
    public void Create_RefusesAMalformedCurrency(string currency)
    {
        var act = () => Order.Create("ORD-0001", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: currency, now: Now);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Placing and accepting an order stamp the moment the caller passed on their events.</summary>
    [Fact]
    public void CreateAndAccept_StampTheGivenMomentOnTheirEvents()
    {
        var acceptedAt = Now.AddHours(2);
        var order = Order.Create("ORD-0001", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "USD", now: Now);

        order.DomainEvents.OfType<OrderCreatedEvent>().Single().OccurredAt.Should().Be(Now);

        order.Accept(acceptedAt);

        order.DomainEvents.OfType<OrderAcceptedEvent>().Single().OccurredAt.Should().Be(acceptedAt);
    }

    /// <summary>Accept on a non-Pending order throws <see cref="DomainRuleException"/> with <c>ORDER_NOT_PENDING</c>.</summary>
    [Fact]
    public void Accept_WhenNotPending_ThrowsWithOrderNotPending()
    {
        var order = Order.Create("ORD-0001", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "USD", now: Now);
        order.Accept(Now);

        var act = () => order.Accept(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotPending);
    }

    /// <summary>Cancel on a non-Pending order throws <see cref="DomainRuleException"/> with <c>ORDER_NOT_PENDING</c>.</summary>
    [Fact]
    public void Cancel_WhenNotPending_ThrowsWithOrderNotPending()
    {
        var order = Order.Create("ORD-0002", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "USD", now: Now);
        order.Accept(Now);

        var act = order.Cancel;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotPending);
    }

    /// <summary>MarkFraud on a non-Pending order throws <see cref="DomainRuleException"/> with <c>ORDER_NOT_PENDING</c>.</summary>
    [Fact]
    public void MarkFraud_WhenNotPending_ThrowsWithOrderNotPending()
    {
        var order = Order.Create("ORD-0003", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "USD", now: Now);
        order.Accept(Now);

        var act = order.MarkFraud;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotPending);
    }

    /// <summary>Every new order carries a token, so no order is ever payable by id alone.</summary>
    [Fact]
    public void Create_AlwaysIssuesAPaymentToken()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.False(string.IsNullOrWhiteSpace(order.PaymentToken));
    }

    /// <summary>
    /// Two orders never share a token. A shared or derived token would make one order's token a
    /// key to every other, which is the whole failure this field exists to prevent.
    /// </summary>
    [Fact]
    public void Create_IssuesADifferentTokenPerOrder()
    {
        var first = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);
        var second = Order.Create("ORD-0002", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.NotEqual(first.PaymentToken, second.PaymentToken);
    }

    /// <summary>
    /// The token is long enough that guessing is not a strategy. 32 random bytes encode to 43
    /// base64url characters; a token materially shorter than that would be a silent weakening.
    /// </summary>
    [Fact]
    public void Create_IssuesATokenOfAtLeastFortyThreeCharacters()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.True(
            order.PaymentToken.Length >= 43,
            $"Payment token was {order.PaymentToken.Length} characters; expected at least 43.");
    }

    /// <summary>The order's own token is accepted.</summary>
    [Fact]
    public void MatchesPaymentToken_WithTheIssuedToken_ReturnsTrue()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.True(order.MatchesPaymentToken(order.PaymentToken));
    }

    /// <summary>Another order's token is refused.</summary>
    [Fact]
    public void MatchesPaymentToken_WithAnotherOrdersToken_ReturnsFalse()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);
        var other = Order.Create("ORD-0002", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.False(order.MatchesPaymentToken(other.PaymentToken));
    }

    /// <summary>
    /// A caller that sends no token is refused. This is the case that matters most: an outdated
    /// client, or an attacker walking the id range, arrives with none and must not be treated as
    /// authorised.
    /// </summary>
    /// <param name="token">The absent or blank token a caller might send.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MatchesPaymentToken_WithNoToken_ReturnsFalse(string? token)
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.False(order.MatchesPaymentToken(token));
    }

    /// <summary>A token that is merely a prefix of the real one is refused.</summary>
    [Fact]
    public void MatchesPaymentToken_WithAPrefixOfTheToken_ReturnsFalse()
    {
        var order = Order.Create("ORD-0001", 1, PaymentMethodNames.Card, null, "USD", Now);

        Assert.False(order.MatchesPaymentToken(order.PaymentToken[..^1]));
    }

    /// <summary>Backdating moves the placement date.</summary>
    [Fact]
    public void Backdate_MovesCreatedAt()
    {
        var order = Order.Create("DEMO-1", 1, "card", null, "USD", Now);
        var earlier = Now.AddMonths(-5);

        order.Backdate(earlier);

        order.CreatedAt.Should().Be(earlier);
    }

    /// <summary>Only a Pending or Cancelled order can be deleted; an accepted or fraud-flagged one is kept.</summary>
    [Fact]
    public void CanBeDeleted_OnlyForPendingOrCancelled()
    {
        var pending = NewOrder();
        var cancelled = NewOrder();
        cancelled.Cancel();
        var accepted = NewOrder();
        accepted.Accept(Now);
        var fraud = NewOrder();
        fraud.MarkFraud();

        pending.CanBeDeleted.Should().BeTrue();
        cancelled.CanBeDeleted.Should().BeTrue();
        accepted.CanBeDeleted.Should().BeFalse();
        fraud.CanBeDeleted.Should().BeFalse();
    }

    /// <summary>A fresh accepted order has lines nobody has tried yet, and stays Active until an outcome says otherwise.</summary>
    [Fact]
    public void Accept_LeavesTheLinesPendingAndTheOrderActive()
    {
        var order = NewAcceptedOrder();

        order.Status.Should().Be(OrderStatus.Active);
        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Pending && i.FulfilmentAttempts == 0);
    }

    /// <summary>
    /// Beginning fulfilment claims every pending line before any work starts: each is in progress with its first attempt
    /// counted, and the order is Incomplete until the outcomes are recorded.
    /// </summary>
    [Fact]
    public void BeginFulfilment_ClaimsEveryPendingLineAndMakesTheOrderIncomplete()
    {
        var order = NewAcceptedOrder();

        order.BeginFulfilment(Now);

        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.InProgress
            && i.FulfilmentAttempts == 1
            && i.LastFulfilmentAttemptAt == Now);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A claim for one line leaves the other lines alone, and a line a claim already holds is not claimed twice.</summary>
    [Fact]
    public void BeginFulfilment_ForOneLine_ClaimsOnlyThatLineAndNeverTwice()
    {
        var order = NewAcceptedOrder();

        order.BeginFulfilment(Now, onlyItemId: 2);
        order.BeginFulfilment(Now.AddMinutes(1), onlyItemId: 2);

        order.Items.Single(i => i.Id == 1).FulfilmentState.Should().Be(OrderItemFulfilmentState.Pending);
        var claimed = order.Items.Single(i => i.Id == 2);
        claimed.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        claimed.FulfilmentAttempts.Should().Be(1);
        claimed.LastFulfilmentAttemptAt.Should().Be(Now);
    }

    /// <summary>A failed line is claimed again by a run over the whole order, with a new attempt.</summary>
    [Fact]
    public void BeginFulfilment_ClaimsAFailedLineAgainWithANewAttempt()
    {
        var order = NewIncompleteOrder();

        order.BeginFulfilment(Now.AddMinutes(1));

        order.Items.Single(i => i.Id == 1).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        var again = order.Items.Single(i => i.Id == 2);
        again.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        again.FulfilmentAttempts.Should().Be(2);
    }

    /// <summary>The status of an order that was not accepted is the admin's decision: claiming its lines never changes it.</summary>
    [Fact]
    public void BeginFulfilment_LeavesAPendingOrdersStatusAlone()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        GiveIds(order);

        order.BeginFulfilment(Now);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Single().FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
    }

    /// <summary>A recorded success marks the line Fulfilled, dates it and lets the order return to Active once no line is open.</summary>
    [Fact]
    public void RecordLineFulfilled_MarksTheLineAndKeepsTheOrderActive()
    {
        var order = NewClaimedOrder();

        order.RecordLineFulfilled(1, 1, Now);
        order.Status.Should().Be(OrderStatus.Incomplete);
        order.RecordLineFulfilled(2, 1, Now.AddMinutes(1));

        var line = order.Items.Single(i => i.Id == 1);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        line.FulfilmentErrorCode.Should().BeNull();
        line.FulfilmentAttempts.Should().Be(1);
        line.LastFulfilmentAttemptAt.Should().Be(Now);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>A recorded failure keeps the code and makes the accepted order Incomplete, not Active.</summary>
    [Fact]
    public void RecordLineFailed_KeepsTheCodeAndMakesTheOrderIncomplete()
    {
        var order = NewClaimedOrder();

        order.RecordLineFulfilled(1, 1, Now);
        order.RecordLineFailed(2, 1, "DOMAIN_ALREADY_REGISTERED", Now);

        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be("DOMAIN_ALREADY_REGISTERED");
        line.FulfilmentAttempts.Should().Be(1);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>An error code longer than its column is cut, so recording a failure can never fail the save.</summary>
    [Fact]
    public void RecordLineFailed_CutsACodeLongerThanTheColumn()
    {
        var order = NewClaimedOrder();

        order.RecordLineFailed(1, 1, new string('X', OrderItem.FulfilmentErrorCodeMaxLength + 20), Now);

        order.Items.Single(i => i.Id == 1).FulfilmentErrorCode!.Length.Should().Be(OrderItem.FulfilmentErrorCodeMaxLength);
    }

    /// <summary>
    /// An outcome is recorded only for a line a claim holds: a line nobody claimed has no attempt to record, so a stray
    /// outcome cannot invent one.
    /// </summary>
    [Fact]
    public void RecordLineFulfilled_OnAnUnclaimedLine_Throws()
    {
        var order = NewAcceptedOrder();

        var act = () => order.RecordLineFulfilled(1, 1, Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        order.CanRecordLineOutcome(1, 1).Should().BeFalse();
        order.Items.Single(i => i.Id == 1).FulfilmentState.Should().Be(OrderItemFulfilmentState.Pending);
    }

    /// <summary>
    /// A late outcome of an attempt whose claim ran out and was taken over is refused, so it cannot overwrite the result
    /// of the newer attempt that holds the line now.
    /// </summary>
    [Fact]
    public void RecordLineFulfilled_FromAStaleAttempt_IsRefusedAndTheNewerAttemptKeepsTheLine()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now.AddMinutes(1), Lease);
        order.StartLineRetry(2, Now.AddMinutes(12), Lease);

        var late = () => order.RecordLineFulfilled(2, 2, Now.AddMinutes(13));
        var staleFailure = () => order.RecordLineFailed(2, 1, "OLD_FAILURE", Now.AddMinutes(13));

        order.CanRecordLineOutcome(2, 1).Should().BeFalse();
        order.CanRecordLineOutcome(2, 2).Should().BeFalse();
        order.CanRecordLineOutcome(2, 3).Should().BeTrue();
        late.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        staleFailure.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        line.FulfilmentAttempts.Should().Be(3);
        order.RecordLineFulfilled(2, 3, Now.AddMinutes(14));
        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
    }

    /// <summary>An outcome cannot be recorded twice for one attempt: the second finds the line no longer in progress.</summary>
    [Fact]
    public void RecordLineFailed_TwiceForOneAttempt_RefusesTheSecond()
    {
        var order = NewClaimedOrder();
        order.RecordLineFailed(1, 1, "ORDER_LINE_INTERRUPTED", Now);

        var again = () => order.RecordLineFailed(1, 1, "ORDER_LINE_INTERRUPTED", Now.AddMinutes(1));

        again.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        order.Items.Single(i => i.Id == 1).FulfilmentAttempts.Should().Be(1);
    }

    /// <summary>Lines seeded from history read as one finished attempt, and the order is complete.</summary>
    [Fact]
    public void MarkLinesFulfilledFromHistory_MakesThePendingLinesFulfilled()
    {
        var order = NewAcceptedOrder();

        order.MarkLinesFulfilledFromHistory(Now);

        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Fulfilled && i.FulfilmentAttempts == 1);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>A retry claims the failed line: it is in progress, the attempt is counted, and the order is still Incomplete.</summary>
    [Fact]
    public void StartLineRetry_ClaimsTheFailedLineAndCountsTheAttempt()
    {
        var order = NewIncompleteOrder();
        var later = Now.AddMinutes(1);

        order.StartLineRetry(2, later, Lease);

        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        line.FulfilmentAttempts.Should().Be(2);
        line.LastFulfilmentAttemptAt.Should().Be(later);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A retry that succeeds counts one attempt, not two, clears the code and returns the order to Active.</summary>
    [Fact]
    public void RecordLineFulfilled_AfterARetry_ClearsTheCodeAndReturnsTheOrderToActive()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now.AddMinutes(1), Lease);

        order.RecordLineFulfilled(2, 2, Now.AddMinutes(2));

        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        line.FulfilmentErrorCode.Should().BeNull();
        line.FulfilmentAttempts.Should().Be(2);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>A retry that fails again keeps the order Incomplete with the new code and counts one more attempt.</summary>
    [Fact]
    public void RecordLineFailed_AfterARetry_KeepsTheOrderIncomplete()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now.AddMinutes(1), Lease);

        order.RecordLineFailed(2, 2, "REGISTRAR_DOMAIN_UNAVAILABLE", Now.AddMinutes(2));

        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be("REGISTRAR_DOMAIN_UNAVAILABLE");
        line.FulfilmentAttempts.Should().Be(2);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A fulfilled line cannot be retried, so it cannot be fulfilled twice.</summary>
    [Fact]
    public void StartLineRetry_OnAFulfilledLine_Throws()
    {
        var order = NewIncompleteOrder();

        var act = () => order.StartLineRetry(1, Now, Lease);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        order.CanRetryLine(1, Now, Lease).Should().BeFalse();
    }

    /// <summary>A line another retry holds cannot be claimed again until its claim has run out.</summary>
    [Fact]
    public void StartLineRetry_OnAClaimedLine_ThrowsUntilTheClaimExpires()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now, Lease);

        var act = () => order.StartLineRetry(2, Now.AddMinutes(1), Lease);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        order.CanRetryLine(2, Now.AddHours(1), Lease).Should().BeTrue();
    }

    /// <summary>A pending (not accepted) order's lines cannot be retried.</summary>
    [Fact]
    public void StartLineRetry_OnAPendingOrder_Throws()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        GiveIds(order);
        SetLineState(order, 1, OrderItemFulfilmentState.Failed);

        var act = () => order.StartLineRetry(1, Now, Lease);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
    }

    /// <summary>A line the order does not hold is refused, both for a retry and for a recorded outcome.</summary>
    [Fact]
    public void AnUnknownLine_IsRefused()
    {
        var order = NewIncompleteOrder();

        order.CanRetryLine(99, Now, Lease).Should().BeFalse();
        var retry = () => order.StartLineRetry(99, Now, Lease);
        var record = () => order.RecordLineFulfilled(99, 1, Now);

        retry.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        record.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderItemNotFound);
    }

    /// <summary>A claim older than the lease is taken for a dead run and may be taken over, however long or short the lease is.</summary>
    [Fact]
    public void CanRetryLine_OnAClaimedLine_FollowsTheLeaseItIsAsked()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now, Lease);

        order.CanRetryLine(2, Now.AddMinutes(5), Lease).Should().BeFalse();
        order.CanRetryLine(2, Now.AddMinutes(5), TimeSpan.FromMinutes(2)).Should().BeTrue();
        order.CanRetryLine(2, Now.AddMinutes(10), Lease).Should().BeTrue();
    }

    /// <summary>A refund is claimed from a failed line: the line is Refunding, no retry can take it, and the order stays Incomplete.</summary>
    [Fact]
    public void BeginLineRefund_ClaimsTheFailedLineAgainstARetry()
    {
        var order = NewIncompleteOrder();

        order.BeginLineRefund(2);

        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);
        order.CanRetryLine(2, Now.AddHours(1), Lease).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>Only a failed line can start being refunded: a fulfilled, claimed or already refunding line is refused.</summary>
    [Fact]
    public void BeginLineRefund_OnALineThatIsNotFailed_Throws()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now, Lease);

        order.CanRefundLine(1).Should().BeFalse();
        order.CanRefundLine(2).Should().BeFalse();
        var fulfilled = () => order.BeginLineRefund(1);
        var claimed = () => order.BeginLineRefund(2);

        fulfilled.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
        claimed.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
    }

    /// <summary>A refunded line is closed for good: it cannot be retried and no longer keeps the order Incomplete.</summary>
    [Fact]
    public void RecordLineRefunded_ClosesTheLineAndLetsTheOrderReturnToActive()
    {
        var order = NewIncompleteOrder();
        order.BeginLineRefund(2);

        order.RecordLineRefunded(2);

        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.CanRetryLine(2, Now.AddHours(1), Lease).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>A refund that was never begun cannot be recorded as complete.</summary>
    [Fact]
    public void RecordLineRefunded_OnALineNotBeingRefunded_Throws()
    {
        var order = NewIncompleteOrder();

        var act = () => order.RecordLineRefunded(2);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
    }

    /// <summary>A refund that returned nothing puts the line back to failed: it can be retried again and the order stays Incomplete.</summary>
    [Fact]
    public void RecordLineRefundReleased_WithNothingRefunded_ReturnsTheLineToFailed()
    {
        var order = NewIncompleteOrder();
        order.BeginLineRefund(2);

        order.RecordLineRefundReleased(2, partlyRefunded: false);

        var line = order.Items.Single(i => i.Id == 2);
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be("DOMAIN_ALREADY_REGISTERED");
        order.CanRetryLine(2, Now, Lease).Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>
    /// A refund that returned only part is not a refunded line: it stays closed to retries, keeps the order Incomplete,
    /// and is the state staff finish by hand.
    /// </summary>
    [Fact]
    public void RecordLineRefundReleased_WithPartRefunded_KeepsTheLineForManualAction()
    {
        var order = NewIncompleteOrder();
        order.BeginLineRefund(2);

        order.RecordLineRefundReleased(2, partlyRefunded: true);

        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.PartiallyRefunded);
        order.CanRetryLine(2, Now.AddHours(1), Lease).Should().BeFalse();
        order.CanRefundLine(2).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A refund that is settled twice, or never begun, cannot be released.</summary>
    [Fact]
    public void RecordLineRefundReleased_OnALineNotBeingRefunded_Throws()
    {
        var order = NewIncompleteOrder();

        var act = () => order.RecordLineRefundReleased(2, partlyRefunded: false);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
    }

    /// <summary>
    /// A late outcome of an attempt the refund overtook is refused, so a line that is being or was refunded can never
    /// become a delivered one.
    /// </summary>
    [Fact]
    public void RecordLineFulfilled_OnALineBeingRefunded_Throws()
    {
        var order = NewIncompleteOrder();
        order.BeginLineRefund(2);

        var fulfilled = () => order.RecordLineFulfilled(2, 1, Now);
        var failed = () => order.RecordLineFailed(2, 1, null, Now);

        order.CanRecordLineOutcome(2, 1).Should().BeFalse();
        fulfilled.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        failed.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineChanged);
        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);
    }

    /// <summary>
    /// Staff finishing a partly done refund by hand close the line: it is Refunded for good, and the order is complete again
    /// because no other line is open.
    /// </summary>
    [Fact]
    public void CloseLineRefund_OnAPartlyRefundedLine_RefundsItAndLetsTheOrderReturnToActive()
    {
        var order = NewIncompleteOrder();
        order.BeginLineRefund(2);
        order.RecordLineRefundReleased(2, partlyRefunded: true);
        order.CanCloseLineRefund(2).Should().BeTrue();

        order.CloseLineRefund(2);

        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.CanCloseLineRefund(2).Should().BeFalse();
        order.CanRetryLine(2, Now.AddHours(1), Lease).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>The order stays Incomplete when closing a refund leaves another line open.</summary>
    [Fact]
    public void CloseLineRefund_WithAnotherLineOpen_KeepsTheOrderIncomplete()
    {
        var order = NewClaimedOrder();
        order.RecordLineFailed(1, 1, "A", Now);
        order.RecordLineFailed(2, 1, "B", Now);
        order.BeginLineRefund(2);
        order.RecordLineRefundReleased(2, partlyRefunded: true);

        order.CloseLineRefund(2);

        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>Only a partly refunded line can be closed: a failed, refunding, fulfilled or unknown line is refused.</summary>
    [Fact]
    public void CloseLineRefund_OnALineThatIsNotPartlyRefunded_Throws()
    {
        var order = NewIncompleteOrder();

        var failed = () => order.CloseLineRefund(2);
        var fulfilled = () => order.CloseLineRefund(1);
        var unknown = () => order.CloseLineRefund(99);

        order.CanCloseLineRefund(2).Should().BeFalse();
        order.CanCloseLineRefund(99).Should().BeFalse();
        failed.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
        fulfilled.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRefundable);
        unknown.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderItemNotFound);
    }

    /// <summary>
    /// Two lines finishing at the same moment each compute the status from a copy that still shows the other open, so
    /// both leave Incomplete; refreshing the order loaded afresh settles it Active.
    /// </summary>
    [Fact]
    public void RefreshStatus_SettlesAnOrderWhoseEveryLineIsDone()
    {
        var order = NewClaimedOrder();
        order.RecordLineFailed(1, 1, "A", Now);
        order.RecordLineFailed(2, 1, "B", Now);
        order.StartLineRetry(1, Now, Lease);
        order.StartLineRetry(2, Now, Lease);
        var line = typeof(OrderItem).GetProperty(nameof(OrderItem.FulfilmentState))!;
        line.SetValue(order.Items.Single(i => i.Id == 1), OrderItemFulfilmentState.Fulfilled);
        line.SetValue(order.Items.Single(i => i.Id == 2), OrderItemFulfilmentState.Fulfilled);
        order.Status.Should().Be(OrderStatus.Incomplete);

        order.RefreshStatus();

        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>A refresh never changes a status that is the admin's decision: a pending order stays pending.</summary>
    [Fact]
    public void RefreshStatus_LeavesAPendingOrderPending()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        GiveIds(order);

        order.RefreshStatus();

        order.Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>Builds an accepted order with two lines, ids 1 and 2.</summary>
    /// <returns>The order, Active, both lines Pending.</returns>
    private static Order NewAcceptedOrder()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        order.AddItem(4, "Domain", BillingCycle.Annual, 12m, 12m, domain: "example.test", hostname: null);
        GiveIds(order);
        order.Accept(Now);
        return order;
    }

    /// <summary>Builds an accepted order whose two lines a run has claimed: in progress under their first attempt.</summary>
    /// <returns>The order, Incomplete.</returns>
    private static Order NewClaimedOrder()
    {
        var order = NewAcceptedOrder();
        order.BeginFulfilment(Now);
        return order;
    }

    /// <summary>Builds an accepted order whose first line was fulfilled and whose second failed once.</summary>
    /// <returns>The order, Incomplete.</returns>
    private static Order NewIncompleteOrder()
    {
        var order = NewClaimedOrder();
        order.RecordLineFulfilled(1, 1, Now);
        order.RecordLineFailed(2, 1, "DOMAIN_ALREADY_REGISTERED", Now);
        return order;
    }

    /// <summary>Puts a line in a fulfilment state directly, for a state the aggregate cannot reach from the order's own status.</summary>
    /// <param name="order">The order that holds the line.</param>
    /// <param name="itemId">The line.</param>
    /// <param name="state">The state to put it in.</param>
    private static void SetLineState(Order order, int itemId, OrderItemFulfilmentState state)
    {
        typeof(OrderItem).GetProperty(nameof(OrderItem.FulfilmentState))!.SetValue(order.Items.Single(i => i.Id == itemId), state);
    }

    /// <summary>Gives the order's lines the ids 1, 2 and so on that the database would assign, so a line can be named.</summary>
    /// <param name="order">The order whose lines get ids.</param>
    private static void GiveIds(Order order)
    {
        var id = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var next = 1;
        foreach (var item in order.Items)
        {
            id.SetValue(item, next++);
        }
    }

    /// <summary>Builds a pending order to move through its statuses.</summary>
    /// <returns>A new pending order.</returns>
    private static Order NewOrder() =>
        Order.Create("ORD-0001", clientId: 1, paymentMethod: "manual", ipAddress: null, currency: "USD", now: Now);
}
