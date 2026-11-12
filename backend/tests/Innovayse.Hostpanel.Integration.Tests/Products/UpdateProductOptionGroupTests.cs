using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/product-option-groups/{id}</c> against Postgres: the happy path, the missing-group and
/// range refusals, the route id winning over the body, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductOptionGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>PUT without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/product-option-groups/1", new { name = "OS", isRequired = false, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-group-update");

        var response = await client.PutAsJsonAsync(
            "/api/product-option-groups/1", new { name = "OS", isRequired = false, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Updating an unknown group id answers 404 with <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/api/product-option-groups/999999", new { name = "OS", isRequired = false, displayOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
    }

    /// <summary>An update changes the editable fields and keeps the type; the route id wins over a body id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdatesTheGroup_AndTheRouteIdWins()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, ProductOptionType.Quantity, 0, 1, 3);
        var other = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, ProductOptionType.Dropdown, 1);

        var response = await client.PutAsJsonAsync($"/api/product-option-groups/{id}", new
        {
            id = other, name = "Renamed", isRequired = true, displayOrder = 5, minQuantity = 2, maxQuantity = 9,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        var changed = list!.Single(g => g.GetProperty("id").GetInt32() == id);
        changed.GetProperty("name").GetString().Should().Be("Renamed");
        changed.GetProperty("type").GetString().Should().Be(nameof(ProductOptionType.Quantity));
        changed.GetProperty("isRequired").GetBoolean().Should().BeTrue();
        changed.GetProperty("displayOrder").GetInt32().Should().Be(5);
        changed.GetProperty("minQuantity").GetInt32().Should().Be(2);
        changed.GetProperty("maxQuantity").GetInt32().Should().Be(9);
        list!.Single(g => g.GetProperty("id").GetInt32() == other).GetProperty("name").GetString().Should().NotBe("Renamed");
    }

    /// <summary>An inverted range answers 400 with <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c> and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvertedRange_RefusesWithRangeCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, ProductOptionType.Quantity, 0, 1, 3);

        var response = await client.PutAsJsonAsync($"/api/product-option-groups/{id}", new
        {
            name = "Renamed", isRequired = false, displayOrder = 0, minQuantity = 9, maxQuantity = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_QUANTITY_RANGE_INVALID);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        list!.Single().GetProperty("maxQuantity").GetInt32().Should().Be(3);
    }

    /// <summary>A blank name and a negative position are refused as field errors.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidFields_AreRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var blankName = await client.PutAsJsonAsync(
            "/api/product-option-groups/1", new { name = "", isRequired = false, displayOrder = 0 });
        var negativeOrder = await client.PutAsJsonAsync(
            "/api/product-option-groups/1", new { name = "OS", isRequired = false, displayOrder = -1 });

        await ProductsTestSeeds.AssertFieldRefusedAsync(blankName, "name");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativeOrder, "displayOrder");
    }
}
