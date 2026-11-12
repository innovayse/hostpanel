using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Orders.Events;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Orders;

/// <summary>Unit tests for holding an <see cref="Order"/> for fraud review and releasing or refusing it.</summary>
public sealed class OrderFraudReviewTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Makes a pending order with one line.</summary>
    /// <returns>The order.</returns>
    private static Order PendingOrder()
    {
        var order = Order.Create("ORD-0100", clientId: 4, paymentMethod: "manual", ipAddress: "203.0.113.9", currency: "USD", now: Now);
        order.AddItem(1, "Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null);
        return order;
    }

    /// <summary>A new member was added to the status enum without renumbering the ones before it.</summary>
    [Fact]
    public void UnderReview_IsTheNewStatus_AndTheOthersKeepTheirValues()
    {
        ((int)OrderStatus.Pending).Should().Be(0);
        ((int)OrderStatus.Active).Should().Be(1);
        ((int)OrderStatus.Cancelled).Should().Be(2);
        ((int)OrderStatus.Fraud).Should().Be(3);
        ((int)OrderStatus.Incomplete).Should().Be(4);
        ((int)OrderStatus.UnderReview).Should().Be(5);
    }

    /// <summary>A pending order is held with its score and reasons, and a fresh order has neither.</summary>
    [Fact]
    public void HoldForReview_StoresTheScoreAndReasons()
    {
        var order = PendingOrder();
        order.FraudScore.Should().BeNull();
        order.FraudReasons.Should().BeEmpty();

        order.HoldForReview(65, [FraudReasonCode.DisposableEmail, FraudReasonCode.OrderVelocity]);

        order.Status.Should().Be(OrderStatus.UnderReview);
        order.FraudScore.Should().Be(65);
        order.FraudReasons.Should().Equal(FraudReasonCode.DisposableEmail, FraudReasonCode.OrderVelocity);
    }

    /// <summary>Only a pending order can be held.</summary>
    [Fact]
    public void HoldForReview_WhenNotPending_ThrowsWithOrderNotPending()
    {
        var order = PendingOrder();
        order.Accept(Now);

        var act = () => order.HoldForReview(60, []);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotPending);
    }

    /// <summary>A score outside 0 to 100 is refused.</summary>
    /// <param name="score">The score.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void HoldForReview_ScoreOutsideTheScale_Throws(int score)
    {
        var act = () => PendingOrder().HoldForReview(score, []);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>A held order is neither accepted, cancelled nor flagged by the Pending-only transitions, so nothing provisions it.</summary>
    [Fact]
    public void AHeldOrder_CannotBeAcceptedCancelledOrFlagged()
    {
        var order = PendingOrder();
        order.HoldForReview(60, [FraudReasonCode.DisposableEmail]);

        foreach (var act in new Action[] { () => order.Accept(Now), order.Cancel, order.MarkFraud })
        {
            act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotPending);
        }

        order.IsAccepted.Should().BeFalse();
        order.CanBeDeleted.Should().BeFalse();
        order.Invoking(o => o.BeginFulfilment(Now)).Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotAccepted);
    }

    /// <summary>Approving releases the order back to Pending and keeps the score for the record.</summary>
    [Fact]
    public void ApproveReview_ReturnsTheOrderToPending()
    {
        var order = PendingOrder();
        order.HoldForReview(60, [FraudReasonCode.DisposableEmail]);

        order.ApproveReview();

        order.Status.Should().Be(OrderStatus.Pending);
        order.FraudScore.Should().Be(60);
        order.Accept(Now);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>Only an order under review can be approved.</summary>
    [Fact]
    public void ApproveReview_WhenNotUnderReview_ThrowsWithOrderNotUnderReview()
    {
        var act = PendingOrder().ApproveReview;

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotUnderReview);
    }

    /// <summary>Rejecting flags the order as fraud, cancels its lines and raises the event that mails the client the reason.</summary>
    [Fact]
    public void RejectFromReview_FlagsTheOrderAndRaisesTheEvent()
    {
        var order = PendingOrder();
        order.HoldForReview(70, []);

        order.RejectFromReview("  Payment details could not be verified.  ", Now);

        order.Status.Should().Be(OrderStatus.Fraud);
        order.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Cancelled);
        var raised = order.DomainEvents.OfType<OrderRejectedFromReviewEvent>().Single();
        raised.Reason.Should().Be("Payment details could not be verified.");
        raised.OrderNumber.Should().Be("ORD-0100");
        raised.ClientId.Should().Be(4);
        raised.OccurredAt.Should().Be(Now);
    }

    /// <summary>Only an order under review can be rejected from the queue.</summary>
    [Fact]
    public void RejectFromReview_WhenNotUnderReview_ThrowsWithOrderNotUnderReview()
    {
        var act = () => PendingOrder().RejectFromReview("no", Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(OrdersErrors.OrderNotUnderReview);
    }

    /// <summary>A reason is required, because the client reads it.</summary>
    /// <param name="reason">The reason.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectFromReview_WithoutAReason_Throws(string reason)
    {
        var order = PendingOrder();
        order.HoldForReview(70, []);

        var act = () => order.RejectFromReview(reason, Now);

        act.Should().Throw<ArgumentException>();
    }
}
