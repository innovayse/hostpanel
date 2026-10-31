using FluentValidation.Results;
using FluentValidation.TestHelper;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateBillableItem;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;
using Innovayse.Hostpanel.Application.Billing.Currencies.Commands.SetBaseCurrency;
using Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpsertCurrency;
using Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;
using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Domains.Commands.CreateTldConfig;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateTldConfig;
using Innovayse.Hostpanel.Application.Orders.Commands.PlaceOrder;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Products.Commands.UpdateProductPrices;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProducts;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductCyclePrices;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>
/// Pins that every validator of a command or query carrying an ISO 4217 code refuses anything but
/// three ASCII letters (answered as a 400 by the validation pipeline), and accepts a well-formed
/// code in any case. Runs without a database.
/// </summary>
public sealed class CurrencyCodeValidatorTests
{
    /// <summary>Localizer that echoes keys, so no resource file is needed.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>Codes that are not three ASCII letters: digits, punctuation, padding, wrong length, blank and non-ASCII letters.</summary>
    public static TheoryData<string> BadCodes => new()
    {
        "12!",
        "123",
        "U$D",
        " eu",
        "eur ",
        " EU ",
        "US",
        "USDD",
        "",
        "   ",
        "€UR",
        "ЕUR",
    };

    /// <summary>Whether the result holds an error on a property whose path starts with the given name.</summary>
    /// <param name="result">The validation result.</param>
    /// <param name="property">The property path prefix.</param>
    /// <returns><see langword="true"/> when such an error exists.</returns>
    private static bool HasErrorOn(ValidationResult result, string property) =>
        result.Errors.Any(e => e.PropertyName.StartsWith(property, StringComparison.Ordinal));

    /// <summary>Asserts a validation outcome for one property: refused when <paramref name="bad"/>, clean otherwise.</summary>
    /// <param name="result">The validation result.</param>
    /// <param name="property">The property path prefix.</param>
    /// <param name="bad">Whether the code under test is malformed.</param>
    private static void AssertOutcome(ValidationResult result, string property, bool bad) =>
        HasErrorOn(result, property).Should().Be(bad);

    /// <summary>Builds the empty dictionaries a tld command needs.</summary>
    /// <returns>An empty price map.</returns>
    private static Dictionary<int, decimal> Prices() => [];

    /// <summary>A billable item refuses a malformed currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void CreateBillableItem_BadCurrency_IsRefused(string code)
    {
        var result = new CreateBillableItemValidator(Localizer)
            .Validate(new CreateBillableItemCommand(1, "Hosting", 10m, code, BillableItemType.OneTime));

        AssertOutcome(result, "Currency", bad: true);
    }

    /// <summary>A billable item accepts a well-formed currency in any case.</summary>
    /// <param name="code">A well-formed code.</param>
    [Theory]
    [InlineData("usd")]
    [InlineData("EUR")]
    public void CreateBillableItem_GoodCurrency_IsAccepted(string code)
    {
        var result = new CreateBillableItemValidator(Localizer)
            .Validate(new CreateBillableItemCommand(1, "Hosting", 10m, code, BillableItemType.OneTime));

        AssertOutcome(result, "Currency", bad: false);
    }

    /// <summary>An order invoice refuses a malformed currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void CreateOrderInvoice_BadCurrency_IsRefused(string code)
    {
        var result = new CreateOrderInvoiceValidator(Localizer)
            .Validate(new CreateOrderInvoiceCommand(1, code, []));

        AssertOutcome(result, "Currency", bad: true);
    }

    /// <summary>The orderable-products query refuses a malformed currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void GetOrderableProducts_BadCurrency_IsRefused(string code)
    {
        var result = new GetOrderableProductsValidator(Localizer)
            .Validate(new GetOrderableProductsQuery([1], code));

        AssertOutcome(result, "Currency", bad: true);
    }

    /// <summary>The cycle-price lookup refuses a malformed currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void GetProductCyclePrices_BadCurrency_IsRefused(string code)
    {
        var result = new GetProductCyclePricesValidator(Localizer)
            .Validate(new GetProductCyclePricesQuery([new ProductCyclePriceLookup(1, code, BillingCycle.Monthly)]));

        AssertOutcome(result, "Lookups", bad: true);
    }

    /// <summary>A product price row refuses a malformed currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void ProductPrice_BadCurrency_IsRefused(string code)
    {
        var result = new ProductPriceDtoValidator(Localizer)
            .Validate(new ProductPriceDto(code, 1m, null, null, null, null));

        AssertOutcome(result, "CurrencyCode", bad: true);
    }

    /// <summary>The product price recompute refuses a malformed code in the list.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void UpdateProductPrices_BadCurrency_IsRefused(string code)
    {
        var result = new UpdateProductPricesValidator(Localizer)
            .Validate(new UpdateProductPricesCommand(["EUR", code]));

        AssertOutcome(result, "Currencies", bad: true);
    }

    /// <summary>Both currencies of a new tld refuse a malformed code.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void CreateTldConfig_BadCurrencies_AreRefused(string code)
    {
        var result = new CreateTldConfigValidator(Localizer).Validate(new CreateTldConfigCommand(
            "com", "module", code, code, true, 0, [], Prices(), Prices(), Prices(), Prices(), Prices(), Prices()));

        AssertOutcome(result, "Currency", bad: true);
        AssertOutcome(result, "SellCurrency", bad: true);
    }

    /// <summary>Both currencies of an existing tld refuse a malformed code.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void UpdateTldConfig_BadCurrencies_AreRefused(string code)
    {
        var result = new UpdateTldConfigValidator(Localizer).Validate(new UpdateTldConfigCommand(
            1, "module", code, code, true, 0, [], Prices(), Prices(), Prices(), Prices(), Prices(), Prices()));

        AssertOutcome(result, "Currency", bad: true);
        AssertOutcome(result, "SellCurrency", bad: true);
    }

    /// <summary>A tld with well-formed currencies raises no currency error.</summary>
    [Fact]
    public void CreateTldConfig_GoodCurrencies_AreAccepted()
    {
        var result = new CreateTldConfigValidator(Localizer).Validate(new CreateTldConfigCommand(
            "com", "module", "usd", "EUR", true, 0, [], Prices(), Prices(), Prices(), Prices(), Prices(), Prices()));

        AssertOutcome(result, "Currency", bad: false);
        AssertOutcome(result, "SellCurrency", bad: false);
    }

    /// <summary>An order refuses a malformed currency but still accepts none.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void PlaceOrder_BadCurrency_IsRefused(string code)
    {
        var result = new PlaceOrderValidator(new FixedUser("user-1"), Localizer)
            .Validate(Order(code));

        AssertOutcome(result, "Currency", bad: true);
    }

    /// <summary>An order with no currency raises no currency error: the handler chooses one.</summary>
    [Fact]
    public void PlaceOrder_NoCurrency_IsAccepted()
    {
        var result = new PlaceOrderValidator(new FixedUser("user-1"), Localizer)
            .Validate(Order(null));

        AssertOutcome(result, "Currency", bad: false);
    }

    /// <summary>Setting the base currency refuses a malformed code.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void SetBaseCurrency_BadCode_IsRefused(string code)
    {
        var result = new SetBaseCurrencyValidator(Localizer).Validate(new SetBaseCurrencyCommand(code));

        AssertOutcome(result, "Code", bad: true);
    }

    /// <summary>Setting the base currency refuses a missing code.</summary>
    [Fact]
    public void SetBaseCurrency_NullCode_IsRefused()
    {
        var result = new SetBaseCurrencyValidator(Localizer).Validate(new SetBaseCurrencyCommand(null));

        AssertOutcome(result, "Code", bad: true);
    }

    /// <summary>Saving a currency refuses a malformed code.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void UpsertCurrency_BadCode_IsRefused(string code)
    {
        var result = new UpsertCurrencyValidator(Localizer)
            .Validate(new UpsertCurrencyCommand(code, "978", "", "", 2, 1m, true));

        AssertOutcome(result, "Code", bad: true);
    }

    /// <summary>Updating a client refuses a malformed preferred currency.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void UpdateClient_BadCurrency_IsRefused(string code)
    {
        var result = new UpdateClientValidator(Localizer).Validate(new UpdateClientCommand(
            1, null, "Ada", "Lovelace", null, null,
            new PostalAddress(null, null, null, null, null, null),
            new ClientPreferencesDto(null, code, null, null, null),
            new ContactNotifications(true, true, true, true, true, true),
            new ClientSettingsDto(true, false, false, false, false, false, false),
            null));

        AssertOutcome(result, "Preferences.Currency", bad: true);
    }

    /// <summary>Creating a client refuses a malformed preferred currency but accepts none.</summary>
    /// <param name="code">A malformed code.</param>
    [Theory]
    [MemberData(nameof(BadCodes))]
    public void AdminCreateClient_BadCurrency_IsRefused(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            // An empty value means "no preference" on create, the same as a missing one.
            return;
        }

        var result = new AdminCreateClientValidator(Localizer).Validate(new AdminCreateClientCommand(
            CreateNewUser: false, ExistingUserId: "user-1", Email: null,
            Password: null, FirstName: "Ada", LastName: "Lovelace", CompanyName: null, Phone: null,
            Address: new PostalAddress(null, null, null, null, null, null),
            Preferences: new ClientPreferencesDto(null, code, null, null, null), Status: null));

        AssertOutcome(result, "Preferences.Currency", bad: true);
    }

    /// <summary>Builds an order command carrying the given currency.</summary>
    /// <param name="currency">The currency to put on the command, or <see langword="null"/>.</param>
    /// <returns>The command.</returns>
    private static PlaceOrderCommand Order(string? currency) => new(
        "Ada", "Lovelace", "ada@example.test", "Passw0rd!x", null, "banktransfer",
        [new PlaceOrderItemDto(1, BillingCycle.Monthly, null, null)], currency);
}
