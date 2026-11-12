using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/product-option-groups/order</c> against Postgres: the new order is stored, any list that
/// is not exactly the product's groups is refused without changing anything, and the authorization
/// boundary holds.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReorderProductOptionGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the ids of a product's groups in the order the admin list returns them.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="productId">The product to read.</param>
    /// <returns>The group ids, in display order.</returns>
    private static async Task<int[]> GroupOrderAsync(HttpClient client, int productId)
    {
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        return list!.Select(g => g.GetProperty("id").GetInt32()).ToArray();
    }

    /// <summary>PUT without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId = 1, groupIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-group-order");

        var response = await client.PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId = 1, groupIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The list becomes the groups' display order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StoresTheNewOrder()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var first = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 0);
        var second = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 1);
        var third = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 2);

        var response = await client.PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId, groupIds = new[] { third, first, second } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GroupOrderAsync(client, productId)).Should().Equal(third, first, second);
    }

    /// <summary>A list that leaves a group out, repeats one or names another product's group is refused and nothing moves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListThatIsNotExactlyTheProductsGroups_RefusesWithMismatchCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var foreignProductId = await ProductsTestSeeds.CreateProductAsync(client);
        var first = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 0);
        var second = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 1);
        var foreign = await ProductsTestSeeds.CreateOptionGroupAsync(client, foreignProductId);

        var lists = new[]
        {
            new[] { second },
            new[] { second, second },
            new[] { second, first, first },
            new[] { second, foreign },
            new[] { second, first, foreign },
        };

        foreach (var groupIds in lists)
        {
            var response = await client.PutAsJsonAsync("/api/product-option-groups/order", new { productId, groupIds });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_GROUP_ORDER_MISMATCH);
        }

        (await GroupOrderAsync(client, productId)).Should().Equal(first, second);
        (await GroupOrderAsync(client, foreignProductId)).Should().Equal(foreign);
    }

    /// <summary>An empty list, a non-positive id and a missing product are refused by validation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidFields_AreRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var empty = await client.PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId = 1, groupIds = Array.Empty<int>() });
        var badId = await client.PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId = 1, groupIds = new[] { 0 } });
        var noProduct = await client.PutAsJsonAsync(
            "/api/product-option-groups/order", new { productId = 0, groupIds = new[] { 1 } });

        await ProductsTestSeeds.AssertFieldRefusedAsync(empty, "groupIds");
        badId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertFieldRefusedAsync(noProduct, "productId");
    }
}
