namespace Innovayse.Application.Tests.Orders;

using FluentValidation.TestHelper;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Moq;
using Xunit;

/// <summary>
/// The validator refuses a billing cycle the parser would not understand, so a typo in the
/// request is a 400 with a message and never the parser's exception turned into a 500.
/// </summary>
public sealed class PlaceOrderValidatorTests
{
    /// <summary>The validator under test, for a signed-in caller so no registration fields are asked for.</summary>
    private static PlaceOrderValidator Validator()
    {
        var caller = new Mock<ICurrentRequestContext>();
        caller.SetupGet(c => c.UserId).Returns("subject-1");
        return new PlaceOrderValidator(caller.Object);
    }

    /// <summary>An order for one product under the given cycle.</summary>
    private static PlaceOrderCommand OrderWithCycle(string cycle) => new(
        FirstName: null, LastName: null, Email: null, Password: null, Phone: null,
        PaymentMethod: "stripe",
        Items: [new PlaceOrderItemDto(1, cycle, null, null)]);

    [Theory]
    [InlineData("monthly")]
    [InlineData("annual")]
    [InlineData("annually")]
    [InlineData("Monthly")]
    [InlineData("ANNUALLY")]
    public void AKnownCycleInAnyCasePasses(string cycle)
    {
        var result = Validator().TestValidate(OrderWithCycle(cycle));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("weekly")]
    [InlineData("yearly")]
    [InlineData("")]
    public void AnUnknownCycleIsRefused(string cycle)
    {
        var result = Validator().TestValidate(OrderWithCycle(cycle));

        result.ShouldHaveValidationErrorFor("Items[0].BillingCycle");
    }
}
