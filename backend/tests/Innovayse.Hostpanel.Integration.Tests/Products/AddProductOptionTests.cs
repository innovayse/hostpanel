using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/product-options</c> against Postgres: the happy path, the missing-group refusal, each
/// rule the group's type enforces, field validation, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddProductOptionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>POST without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-add");

        var response = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Adding to an unknown group answers 404 with <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownGroup_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 999_999, name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
    }

    /// <summary>An option is created, reported with 201 Created and read back with every field, in display order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddsOptions_InDisplayOrder()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);

        var windows = await client.PostAsJsonAsync("/api/product-options", new
        {
            groupId,
            name = "  Windows  ",
            priceModifier = 12.5m,
            priceType = "Percentage",
            setupFeeModifier = 3m,
            displayOrder = 2,
        });
        var linux = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Linux", 0m, 1);

        windows.StatusCode.Should().Be(HttpStatusCode.Created);
        var windowsId = await windows.Content.ReadFromJsonAsync<int>();
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        var options = list!.Single().GetProperty("options").EnumerateArray().ToArray();
        options.Select(o => o.GetProperty("id").GetInt32()).Should().Equal(linux, windowsId);
        options[1].GetProperty("name").GetString().Should().Be("Windows");
        options[1].GetProperty("priceModifier").GetDecimal().Should().Be(12.5m);
        options[1].GetProperty("priceType").GetString().Should().Be("Percentage");
        options[1].GetProperty("setupFeeModifier").GetDecimal().Should().Be(3m);
        options[1].GetProperty("displayOrder").GetInt32().Should().Be(2);
    }

    /// <summary>A second option on a checkbox group answers 400 with <c>PRODUCT_OPTION_COUNT_NOT_ALLOWED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SecondOptionOnACheckbox_RefusesWithCountCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, "Checkbox");
        await ProductsTestSeeds.AddOptionAsync(client, groupId, "Backups", 3m);

        var response = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId, name = "Daily backups", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_COUNT_NOT_ALLOWED);
    }

    /// <summary>A priced option on a text group answers 400 with <c>PRODUCT_OPTION_PRICE_NOT_ALLOWED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PricedTextOption_RefusesWithPriceCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, "Text");

        var response = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId, name = "Note", priceModifier = 1m, priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_PRICE_NOT_ALLOWED);
    }

    /// <summary>A percentage above 100 answers 400 with <c>PRODUCT_OPTION_PRICE_OUT_OF_RANGE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PercentageAbove100_RefusesWithRangeCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);

        var response = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId, name = "Big", priceModifier = 100.5m, priceType = "Percentage" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_PRICE_OUT_OF_RANGE);
    }

    /// <summary>A blank name, a negative price, an unknown price type and a negative position are refused as field errors.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidFields_AreRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var blankName = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = " ", priceType = "Fixed" });
        var negativePrice = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", priceModifier = -1m, priceType = "Fixed" });
        var negativeSetup = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", setupFeeModifier = -1m, priceType = "Fixed" });
        var badType = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", priceType = 99 });
        var negativeOrder = await client.PostAsJsonAsync(
            "/api/product-options", new { groupId = 1, name = "Linux", priceType = "Fixed", displayOrder = -1 });

        await ProductsTestSeeds.AssertFieldRefusedAsync(blankName, "name");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativePrice, "priceModifier");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativeSetup, "setupFeeModifier");
        await ProductsTestSeeds.AssertFieldRefusedAsync(badType, "priceType");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativeOrder, "displayOrder");
    }
}
