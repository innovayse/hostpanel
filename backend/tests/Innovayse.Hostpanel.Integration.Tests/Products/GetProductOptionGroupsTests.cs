using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/product-option-groups</c> against Postgres: the admin view of a product's groups, ordered,
/// empty groups included, scoped to the product, and closed to everyone but an admin.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductOptionGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET without auth answers 401 Unauthorized: the admin view is not public.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/product-option-groups?productId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-groups-list");

        var response = await client.GetAsync("/api/product-option-groups?productId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A missing or non-positive product id is refused by validation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidProductId_IsRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/product-option-groups?productId=0");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "productId");
    }

    /// <summary>A product that does not exist answers 404 with <c>PRODUCT_NOT_FOUND</c>, as creating a group does.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownProduct_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/product-option-groups?productId=999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A product with no groups reads as an empty list, not as a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProductWithoutGroups_ReturnsEmptyList()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");

        list.Should().BeEmpty();
    }

    /// <summary>
    /// Groups come back by display order with their options by display order, empty groups included,
    /// and another product's groups never appear.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsGroupsAndOptionsInDisplayOrder_ForThatProductOnly()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var otherProductId = await ProductsTestSeeds.CreateProductAsync(client);
        var last = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 5);
        var first = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 1);
        var empty = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, ProductOptionType.Text, displayOrder: 3);
        await ProductsTestSeeds.CreateOptionGroupAsync(client, otherProductId);
        var second = await ProductsTestSeeds.AddOptionAsync(client, first, "B", 0m, 2);
        var firstOption = await ProductsTestSeeds.AddOptionAsync(client, first, "A", 0m, 1);

        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");

        list!.Select(g => g.GetProperty("id").GetInt32()).Should().Equal(first, empty, last);
        list![0].GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetInt32())
            .Should().Equal(firstOption, second);
        list[1].GetProperty("options").GetArrayLength().Should().Be(0);
        list[0].GetProperty("productId").GetInt32().Should().Be(productId);
    }
}
