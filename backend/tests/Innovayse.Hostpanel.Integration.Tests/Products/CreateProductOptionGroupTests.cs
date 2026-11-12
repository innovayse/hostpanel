using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/product-option-groups</c> against Postgres: the happy path, the missing-product and
/// quantity-range refusals, field validation, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProductOptionGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>POST without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/product-option-groups", new { productId = 1, name = "OS", type = nameof(ProductOptionType.Dropdown) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-group-create");

        var response = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId = 1, name = "OS", type = nameof(ProductOptionType.Dropdown) });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Adding a group to an unknown product answers 404 with <c>PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownProduct_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId = 999_999, name = "OS", type = nameof(ProductOptionType.Dropdown) });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A group is created, reported with 201 Created and read back with every field.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreatesAGroup()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PostAsJsonAsync("/api/product-option-groups", new
        {
            productId,
            name = "  Extra IPs  ",
            type = nameof(ProductOptionType.Quantity),
            isRequired = true,
            displayOrder = 3,
            minQuantity = 1,
            maxQuantity = 8,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        var group = list!.Single();
        group.GetProperty("id").GetInt32().Should().Be(id);
        group.GetProperty("name").GetString().Should().Be("Extra IPs");
        group.GetProperty("type").GetString().Should().Be(nameof(ProductOptionType.Quantity));
        group.GetProperty("isRequired").GetBoolean().Should().BeTrue();
        group.GetProperty("displayOrder").GetInt32().Should().Be(3);
        group.GetProperty("minQuantity").GetInt32().Should().Be(1);
        group.GetProperty("maxQuantity").GetInt32().Should().Be(8);
        group.GetProperty("options").GetArrayLength().Should().Be(0);
    }

    /// <summary>A quantity group without a range answers 400 with <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuantityWithoutARange_RefusesWithRangeCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId, name = "IPs", type = nameof(ProductOptionType.Quantity) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_QUANTITY_RANGE_INVALID);
    }

    /// <summary>A list group given a range answers 400 with <c>PRODUCT_OPTION_QUANTITY_RANGE_INVALID</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DropdownWithARange_RefusesWithRangeCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PostAsJsonAsync("/api/product-option-groups", new
        {
            productId, name = "OS", type = nameof(ProductOptionType.Dropdown), minQuantity = 1, maxQuantity = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_QUANTITY_RANGE_INVALID);
    }

    /// <summary>A blank name, an unknown type and a negative position are refused as field errors.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidFields_AreRefusedByField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var blankName = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId, name = " ", type = nameof(ProductOptionType.Dropdown) });
        var badType = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId, name = "OS", type = 99 });
        var negativeOrder = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId, name = "OS", type = nameof(ProductOptionType.Dropdown), displayOrder = -1 });
        var noProduct = await client.PostAsJsonAsync(
            "/api/product-option-groups", new { productId = 0, name = "OS", type = nameof(ProductOptionType.Dropdown) });

        await ProductsTestSeeds.AssertFieldRefusedAsync(blankName, "name");
        await ProductsTestSeeds.AssertFieldRefusedAsync(badType, "type");
        await ProductsTestSeeds.AssertFieldRefusedAsync(negativeOrder, "displayOrder");
        await ProductsTestSeeds.AssertFieldRefusedAsync(noProduct, "productId");
    }
}
