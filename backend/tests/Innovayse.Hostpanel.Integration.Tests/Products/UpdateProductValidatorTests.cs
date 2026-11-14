using FluentValidation.TestHelper;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProduct;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// The SKU, metadata and grace period rules <see cref="UpdateProductValidator"/> applies, without a
/// database: null keeps and blank clears, so both pass; anything else must pass the domain's checks.
/// </summary>
public sealed class UpdateProductValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly UpdateProductValidator _validator = new(new EchoLocalizer<ApplicationValidationMessages>());

    /// <summary>Builds a valid update command with the fields under test overridden.</summary>
    /// <param name="sku">The SKU to put on the command.</param>
    /// <param name="metadata">The metadata to put on the command.</param>
    /// <param name="gracePeriodDays">The grace period to put on the command.</param>
    /// <param name="trial">The free-trial settings to put on the command.</param>
    /// <returns>The command.</returns>
    private static UpdateProductCommand Command(
        string? sku = null, string? metadata = null, int? gracePeriodDays = null, ProductTrialDto? trial = null) => new(
        Id: 1, Name: "Licence", Description: null, Website: null, Slug: null, PackageName: null,
        Prices: [new ProductPriceDto("USD", 9m, null, null, null, null)], ServerGroupId: null,
        Sku: sku, Metadata: metadata, GracePeriodDays: gracePeriodDays, Trial: trial);

    /// <summary>A SKU, a metadata object and an in-range grace period pass.</summary>
    [Fact]
    public void FulfilmentFields_WhenValid_Pass()
    {
        _validator.TestValidate(Command(sku: "lic_pro", metadata: "{}", gracePeriodDays: 0))
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Null (keep) and empty (clear) pass for the SKU and the metadata.</summary>
    /// <param name="value">An absent or empty value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FulfilmentFields_WhenKeptOrCleared_Pass(string? value)
    {
        _validator.TestValidate(Command(sku: value, metadata: value)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A malformed SKU is refused with <c>PRODUCT_SKU_INVALID</c>.</summary>
    [Fact]
    public void Sku_WhenMalformed_IsRefusedWithTheSkuCode()
    {
        _validator.TestValidate(Command(sku: "lic..pro"))
            .ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_SKU_INVALID)
            .WithErrorMessage("text:ProductSkuInvalid");
    }

    /// <summary>Metadata that is not a JSON object is refused with <c>PRODUCT_METADATA_INVALID</c>.</summary>
    [Fact]
    public void Metadata_WhenNotAnObject_IsRefusedWithTheMetadataCode()
    {
        _validator.TestValidate(Command(metadata: "null"))
            .ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithErrorMessage("text:ProductMetadataInvalid");
    }

    /// <summary>Metadata over <see cref="Product.MaxMetadataBytes"/> is refused with the size sentence.</summary>
    [Fact]
    public void Metadata_WhenOverTheSizeCap_IsRefusedWithTheSizeSentence()
    {
        _validator.TestValidate(Command(metadata: ProductsTestSeeds.MetadataOverTheCap()))
            .ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithErrorMessage("text:ProductMetadataTooLarge");
    }

    /// <summary>A grace period outside 0 to 365 days is refused with <c>PRODUCT_GRACE_PERIOD_OUT_OF_RANGE</c>.</summary>
    /// <param name="days">A grace period just outside either bound.</param>
    [Theory]
    [InlineData(Product.MinGracePeriodDays - 1)]
    [InlineData(Product.MaxGracePeriodDays + 1)]
    public void GracePeriod_WhenOutOfRange_IsRefusedWithTheGraceCode(int days)
    {
        _validator.TestValidate(Command(gracePeriodDays: days))
            .ShouldHaveValidationErrorFor(x => x.GracePeriodDays)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_GRACE_PERIOD_OUT_OF_RANGE);
    }

    /// <summary>No trial settings, and settings inside every range, pass.</summary>
    [Fact]
    public void Trial_WhenAbsentOrValid_Passes()
    {
        _validator.TestValidate(Command()).ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(Command(trial: new ProductTrialDto(true, 14, true, [3, 1], 3))).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>Trial settings outside the ranges are refused with <c>PRODUCT_TRIAL_INVALID</c>.</summary>
    /// <param name="days">The trial length.</param>
    /// <param name="grace">The grace period.</param>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(366, 3)]
    [InlineData(14, 31)]
    public void Trial_WhenOutOfRange_IsRefusedWithTheTrialCode(int days, int grace)
    {
        _validator.TestValidate(Command(trial: new ProductTrialDto(true, days, true, [1], grace)))
            .ShouldHaveValidationErrorFor(x => x.Trial)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_TRIAL_INVALID)
            .WithErrorMessage("text:ProductTrialInvalid");
    }

    /// <summary>Reminder days that repeat or fall on or after the end of the trial are refused.</summary>
    /// <param name="reminderDays">The reminder days.</param>
    [Theory]
    [InlineData(new[] { 3, 3 })]
    [InlineData(new[] { 14 })]
    [InlineData(new[] { 0 })]
    public void Trial_WhenReminderDaysAreWrong_IsRefused(int[] reminderDays)
    {
        _validator.TestValidate(Command(trial: new ProductTrialDto(true, 14, true, reminderDays, 3)))
            .ShouldHaveValidationErrorFor(x => x.Trial)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_TRIAL_INVALID);
    }
}
