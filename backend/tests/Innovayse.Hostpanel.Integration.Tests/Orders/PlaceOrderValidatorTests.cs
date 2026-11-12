using FluentValidation.TestHelper;
using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// The shape rule <see cref="PlaceOrderValidator"/> applies to the bundle a checkout names, without a database:
/// whether the bundle exists and matches the lines is the handler's question, only the id's shape is asked here.
/// </summary>
public sealed class PlaceOrderValidatorTests
{
    /// <summary>Validator instance under test, for a signed-in caller so no guest field is required.</summary>
    private readonly PlaceOrderValidator _validator = new(new FixedUser("subject"), new EchoLocalizer<ApplicationValidationMessages>());

    /// <summary>Builds a valid signed-in order with the bundle under test.</summary>
    /// <param name="bundleId">The bundle to put on the command.</param>
    /// <returns>The command.</returns>
    private static PlaceOrderCommand Command(int? bundleId) => new(
        null, null, null, null, null, "bank_transfer",
        [new PlaceOrderItemDto(1, BillingCycle.Monthly, null, null)],
        BundleId: bundleId);

    /// <summary>An order of single products and an order naming a bundle both pass.</summary>
    /// <param name="bundleId">No bundle, or the id of one.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(42)]
    public void BundleId_WhenAbsentOrPositive_Passes(int? bundleId)
    {
        _validator.TestValidate(Command(bundleId)).ShouldNotHaveValidationErrorFor(x => x.BundleId);
    }

    /// <summary>A bundle id that is not positive is refused with the bundle sentence.</summary>
    /// <param name="bundleId">Zero or a negative id.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BundleId_WhenNotPositive_IsRefused(int bundleId)
    {
        _validator.TestValidate(Command(bundleId))
            .ShouldHaveValidationErrorFor(x => x.BundleId)
            .WithErrorMessage("text:PlaceOrderBundleIdInvalid");
    }
}
