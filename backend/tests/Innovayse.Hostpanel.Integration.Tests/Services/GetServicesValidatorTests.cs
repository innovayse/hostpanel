using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Services.Queries.GetServices;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Pins which sentence <see cref="GetServicesValidator"/> asks for on each field of the service list query. The status and
/// billing cycle rules are what answer when a value that is not a member reaches the use case without passing the model
/// binder, which refuses such a value from a request first. Runs without a database.
/// </summary>
public sealed class GetServicesValidatorTests
{
    /// <summary>Answers each message key with a marked copy of the key, so no resource file is needed.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>The validator under test.</summary>
    private static readonly GetServicesValidator Validator = new(Localizer);

    /// <summary>A query with every filter set to a valid value passes.</summary>
    [Fact]
    public void EveryFilterSet_Passes()
    {
        var query = new GetServicesQuery(
            1, 20, "shop", ClientId: 3, Status: ServiceStatus.Active, ProductId: 4, BillingCycle: BillingCycle.Annual);

        Validator.Validate(query).IsValid.Should().BeTrue();
    }

    /// <summary>A query with only the paging window passes: every filter is optional.</summary>
    [Fact]
    public void OnlyThePagingWindow_Passes()
    {
        Validator.Validate(new GetServicesQuery(1, 20)).IsValid.Should().BeTrue();
    }

    /// <summary>A status that is not a member is refused with the service status sentence, under the status field.</summary>
    [Fact]
    public void StatusOutsideTheEnum_IsRefusedWithTheStatusSentence()
    {
        var result = Validator.Validate(new GetServicesQuery(1, 20, Status: (ServiceStatus)99));

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(GetServicesQuery.Status));
        error.ErrorMessage.Should().Be("text:ServiceListStatusInvalid");
    }

    /// <summary>A billing cycle that is not a member is refused with the billing cycle sentence, under its field.</summary>
    [Fact]
    public void BillingCycleOutsideTheEnum_IsRefusedWithTheCycleSentence()
    {
        var result = Validator.Validate(new GetServicesQuery(1, 20, BillingCycle: (BillingCycle)99));

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(nameof(GetServicesQuery.BillingCycle));
        error.ErrorMessage.Should().Be("text:ServiceListBillingCycleInvalid");
    }

    /// <summary>A product or client id that is not positive is refused with its own sentence under its own field.</summary>
    /// <param name="productId">The product id, or null for none.</param>
    /// <param name="clientId">The client id, or null for none.</param>
    /// <param name="field">The field the refusal names.</param>
    /// <param name="sentence">The sentence key the refusal asks for.</param>
    [Theory]
    [InlineData(0, null, nameof(GetServicesQuery.ProductId), "text:ProductIdInvalid")]
    [InlineData(-1, null, nameof(GetServicesQuery.ProductId), "text:ProductIdInvalid")]
    [InlineData(null, 0, nameof(GetServicesQuery.ClientId), "text:ServiceClientIdFilterInvalid")]
    [InlineData(null, -5, nameof(GetServicesQuery.ClientId), "text:ServiceClientIdFilterInvalid")]
    public void NonPositiveId_IsRefusedWithItsOwnSentence(int? productId, int? clientId, string field, string sentence)
    {
        var result = Validator.Validate(new GetServicesQuery(1, 20, ClientId: clientId, ProductId: productId));

        var error = result.Errors.Should().ContainSingle().Which;
        error.PropertyName.Should().Be(field);
        error.ErrorMessage.Should().Be(sentence);
    }

    /// <summary>The page, page size and search are the shared list rules: each refusal names its field and its sentence.</summary>
    [Fact]
    public void PageSizeAndSearch_FollowTheSharedListRules()
    {
        var result = Validator.Validate(new GetServicesQuery(0, 101, new string('a', 201)));

        result.Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Should().BeEquivalentTo(new[]
        {
            (nameof(GetServicesQuery.Page), "text:ListPageRequired"),
            (nameof(GetServicesQuery.PageSize), "text:ListPageSizeRange"),
            (nameof(GetServicesQuery.Search), "text:ListSearchTooLong"),
        });
    }
}
