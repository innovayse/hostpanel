using FluentValidation.TestHelper;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Products.Commands.CreateProduct;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// The SKU, metadata and grace period rules <see cref="CreateProductValidator"/> applies, without a
/// database: each refusal carries the domain's code and the sentence key of its own rule.
/// </summary>
public sealed class CreateProductValidatorTests
{
    /// <summary>Validator instance under test.</summary>
    private readonly CreateProductValidator _validator = new(new EchoLocalizer<ApplicationValidationMessages>());

    /// <summary>Builds a valid digital product command with the fields under test overridden.</summary>
    /// <param name="sku">The SKU to put on the command.</param>
    /// <param name="metadata">The metadata to put on the command.</param>
    /// <param name="gracePeriodDays">The grace period to put on the command.</param>
    /// <param name="trial">The free-trial settings to put on the command.</param>
    /// <returns>The command.</returns>
    private static CreateProductCommand Command(
        string? sku = null, string? metadata = null, int? gracePeriodDays = null, ProductTrialDto? trial = null) => new(
        GroupId: 1, Name: "Licence", Description: null, Website: null, Slug: null, PackageName: null,
        Type: ProductType.Digital, Prices: [new ProductPriceDto("USD", 9m, null, null, null, null)], ServerGroupId: null,
        Sku: sku, Metadata: metadata, GracePeriodDays: gracePeriodDays, Trial: trial);

    /// <summary>A digital product with a SKU, a metadata object and an in-range grace period passes.</summary>
    [Fact]
    public void FulfilmentFields_WhenValid_Pass()
    {
        _validator.TestValidate(Command(sku: " lic-pro.v2 ", metadata: "{\"tier\":\"pro\"}", gracePeriodDays: 30))
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>No SKU, no metadata and no grace period pass: the product starts without them and with the default grace.</summary>
    /// <param name="blank">An absent or blank value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FulfilmentFields_WhenAbsentOrBlank_Pass(string? blank)
    {
        _validator.TestValidate(Command(sku: blank, metadata: blank)).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A SKU that fails <see cref="ProductSku.IsValid"/> is refused with <c>PRODUCT_SKU_INVALID</c>.</summary>
    /// <param name="sku">A SKU with a forbidden character, a doubled separator or an edge separator.</param>
    [Theory]
    [InlineData("LIC PRO")]
    [InlineData("LIC--PRO")]
    [InlineData("-LIC")]
    [InlineData("LIC/PRO")]
    public void Sku_WhenMalformed_IsRefusedWithTheSkuCode(string sku)
    {
        _validator.TestValidate(Command(sku: sku))
            .ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_SKU_INVALID)
            .WithErrorMessage("text:ProductSkuInvalid");
    }

    /// <summary>A SKU one character over <see cref="ProductSku.MaxLength"/> is refused.</summary>
    [Fact]
    public void Sku_WhenTooLong_IsRefusedWithTheSkuCode()
    {
        _validator.TestValidate(Command(sku: new string('A', ProductSku.MaxLength + 1)))
            .ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_SKU_INVALID);
    }

    /// <summary>Metadata that is not a JSON object is refused with <c>PRODUCT_METADATA_INVALID</c>.</summary>
    /// <param name="metadata">An array, a scalar or text that is not JSON.</param>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("42")]
    [InlineData("{\"tier\": }")]
    public void Metadata_WhenNotAnObject_IsRefusedWithTheMetadataCode(string metadata)
    {
        _validator.TestValidate(Command(metadata: metadata))
            .ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithErrorMessage("text:ProductMetadataInvalid");
    }

    /// <summary>
    /// Metadata over <see cref="Product.MaxMetadataBytes"/> is refused once, with the size sentence: the
    /// shape rule is never reached, so an oversized body is not parsed.
    /// </summary>
    [Fact]
    public void Metadata_WhenOverTheSizeCap_IsRefusedOnceWithTheSizeSentence()
    {
        var result = _validator.TestValidate(Command(metadata: ProductsTestSeeds.MetadataOverTheCap()));

        result.ShouldHaveValidationErrorFor(x => x.Metadata)
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_METADATA_INVALID)
            .WithErrorMessage("text:ProductMetadataTooLarge");
        result.Errors.Should().ContainSingle();
    }

    /// <summary>Metadata of exactly <see cref="Product.MaxMetadataBytes"/> passes.</summary>
    [Fact]
    public void Metadata_AtTheSizeCap_Passes()
    {
        _validator.TestValidate(Command(metadata: ProductsTestSeeds.MetadataOfBytes(Product.MaxMetadataBytes)))
            .ShouldNotHaveAnyValidationErrors();
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
            .WithErrorCode(HostpanelErrorCodes.PRODUCT_GRACE_PERIOD_OUT_OF_RANGE)
            .WithErrorMessage("text:ProductGracePeriodOutOfRange");
    }

    /// <summary>Both bounds of the grace period are accepted.</summary>
    /// <param name="days">A grace period on either bound.</param>
    [Theory]
    [InlineData(Product.MinGracePeriodDays)]
    [InlineData(Product.MaxGracePeriodDays)]
    public void GracePeriod_OnTheBounds_Passes(int days)
    {
        _validator.TestValidate(Command(gracePeriodDays: days)).ShouldNotHaveAnyValidationErrors();
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
