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

    /// <summary>A recorded success marks the line Fulfilled, counts the attempt, dates it and keeps the order Active.</summary>
    [Fact]
    public void RecordLineFulfilled_MarksTheLineAndKeepsTheOrderActive()
    {
        var order = NewAcceptedOrder();

        order.RecordLineFulfilled(1, Now);

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
        var order = NewAcceptedOrder();

        order.RecordLineFulfilled(1, Now);
        order.RecordLineFailed(2, "DOMAIN_ALREADY_REGISTERED", Now);

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
        var order = NewAcceptedOrder();

        order.RecordLineFailed(1, new string('X', OrderItem.FulfilmentErrorCodeMaxLength + 20), Now);

        order.Items.Single(i => i.Id == 1).FulfilmentErrorCode!.Length.Should().Be(OrderItem.FulfilmentErrorCodeMaxLength);
    }

    /// <summary>A pending order's status is the admin's decision and a recorded outcome never changes it.</summary>
    [Fact]
    public void RecordLineFailed_LeavesAPendingOrderPending()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        GiveIds(order);

        order.RecordLineFailed(1, null, Now);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Single().FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
    }

    /// <summary>A retry claims the failed line: it is in progress, the attempt is counted, and the order is still Incomplete.</summary>
    [Fact]
    public void StartLineRetry_ClaimsTheFailedLineAndCountsTheAttempt()
    {
        var order = NewIncompleteOrder();
        var later = Now.AddMinutes(1);

        order.StartLineRetry(2, later);

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
        order.StartLineRetry(2, Now.AddMinutes(1));

        order.RecordLineFulfilled(2, Now.AddMinutes(2));

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
        order.StartLineRetry(2, Now.AddMinutes(1));

        order.RecordLineFailed(2, "REGISTRAR_DOMAIN_UNAVAILABLE", Now.AddMinutes(2));

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

        var act = () => order.StartLineRetry(1, Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        order.CanRetryLine(1, Now).Should().BeFalse();
    }

    /// <summary>A line another retry holds cannot be claimed again until its claim has run out.</summary>
    [Fact]
    public void StartLineRetry_OnAClaimedLine_ThrowsUntilTheClaimExpires()
    {
        var order = NewIncompleteOrder();
        order.StartLineRetry(2, Now);

        var act = () => order.StartLineRetry(2, Now.AddMinutes(1));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        order.CanRetryLine(2, Now.AddHours(1)).Should().BeTrue();
    }

    /// <summary>A pending (not accepted) order's lines cannot be retried.</summary>
    [Fact]
    public void StartLineRetry_OnAPendingOrder_Throws()
    {
        var order = NewOrder();
        order.AddItem(3, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        GiveIds(order);
        order.RecordLineFailed(1, null, Now);

        var act = () => order.StartLineRetry(1, Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
    }

    /// <summary>A line the order does not hold is refused, both for a retry and for a recorded outcome.</summary>
    [Fact]
    public void AnUnknownLine_IsRefused()
    {
        var order = NewIncompleteOrder();

        order.CanRetryLine(99, Now).Should().BeFalse();
        var retry = () => order.StartLineRetry(99, Now);
        var record = () => order.RecordLineFulfilled(99, Now);

        retry.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderLineNotRetryable);
        record.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderItemNotFound);
    }

    /// <summary>A refunded line is closed for good: it cannot be retried and no longer keeps the order Incomplete.</summary>
    [Fact]
    public void RecordLineRefunded_ClosesTheLineAndLetsTheOrderReturnToActive()
    {
        var order = NewIncompleteOrder();

        order.RecordLineRefunded(2);

        order.Items.Single(i => i.Id == 2).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.CanRetryLine(2, Now.AddHours(1)).Should().BeFalse();
        order.Status.Should().Be(OrderStatus.Active);
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

    /// <summary>Builds an accepted order whose first line was fulfilled and whose second failed once.</summary>
    /// <returns>The order, Incomplete.</returns>
    private static Order NewIncompleteOrder()
    {
        var order = NewAcceptedOrder();
        order.RecordLineFulfilled(1, Now);
        order.RecordLineFailed(2, "DOMAIN_ALREADY_REGISTERED", Now);
        return order;
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
