using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/product-options/{id}</c> against Postgres: the happy path, the missing-option refusal,
/// the rules the group's type enforces, the route id winning over the body, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductOptionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>PUT without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/product-options/1", new { name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-update");

        var response = await client.PutAsJsonAsync(
            "/api/product-options/1", new { name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Updating an unknown option id answers 404 with <c>PRODUCT_OPTION_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/api/product-options/999999", new { name = "Linux", priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_NOT_FOUND);
    }

    /// <summary>An update replaces every field of the option; the route id wins over a body id and a sibling is untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdatesTheOption_AndTheRouteIdWins()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);
        var id = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Linux", 1m, 0);
        var other = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Windows", 2m, 1);

        var response = await client.PutAsJsonAsync($"/api/product-options/{id}", new
        {
            id = other,
            name = "Ubuntu",
            priceModifier = 20m,
            priceType = "Percentage",
            setupFeeModifier = 5m,
            displayOrder = 7,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        var options = list!.Single().GetProperty("options").EnumerateArray().ToArray();
        var changed = options.Single(o => o.GetProperty("id").GetInt32() == id);
        changed.GetProperty("name").GetString().Should().Be("Ubuntu");
        changed.GetProperty("priceModifier").GetDecimal().Should().Be(20m);
        changed.GetProperty("priceType").GetString().Should().Be("Percentage");
        changed.GetProperty("setupFeeModifier").GetDecimal().Should().Be(5m);
        changed.GetProperty("displayOrder").GetInt32().Should().Be(7);
        options.Single(o => o.GetProperty("id").GetInt32() == other).GetProperty("name").GetString().Should().Be("Windows");
    }

    /// <summary>A price on a text option answers 400 with <c>PRODUCT_OPTION_PRICE_NOT_ALLOWED</c> and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PricingATextOption_RefusesWithPriceCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, "Text");
        var id = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Note");

        var response = await client.PutAsJsonAsync(
            $"/api/product-options/{id}", new { name = "Note", priceModifier = 4m, priceType = "Fixed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_PRICE_NOT_ALLOWED);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        list!.Single().GetProperty("options")[0].GetProperty("priceModifier").GetDecimal().Should().Be(0m);
    }

    /// <summary>A percentage above 100 answers 400 with <c>PRODUCT_OPTION_PRICE_OUT_OF_RANGE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PercentageAbove100_RefusesWithRangeCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);
        var id = await ProductsTestSeeds.AddOptionAsync(client, groupId);

        var response = await client.PutAsJsonAsync(
            $"/api/product-options/{id}", new { name = "Big", setupFeeModifier = 101m, priceType = "Percentage" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_PRICE_OUT_OF_RANGE);
    }

    /// <summary>A blank name and a negative price are refused as field errors.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidFields_AreRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var blankName = await client.PutAsJsonAsync("/api/product-options/1", new { name = "", priceType = "Fixed" });
        var negativePrice = await client.PutAsJsonAsync(
            "/api/product-options/1", new { name = "Linux", priceModifier = -1m, priceType = "Fixed" });

        await ProductsTestSeeds.AssertFieldRefusedAsync(blankName, "name");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativePrice, "priceModifier");
    }
}
