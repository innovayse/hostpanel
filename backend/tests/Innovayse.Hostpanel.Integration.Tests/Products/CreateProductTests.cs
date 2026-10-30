using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/products</c> against Postgres: the happy path, the per-currency price list,
/// the fulfilment fields (SKU, metadata, grace period, the Digital type), the missing-group and
/// taken-SKU refusals and the authorization boundary.
/// </summary>
/// <remarks>
/// Pins how <c>CreateProductHandler</c> resolves the per-currency price list against the legacy
/// single-currency pair, and the refusals <c>PRODUCT_GROUP_NOT_FOUND</c> (404) and
/// <c>PRODUCT_SKU_ALREADY_IN_USE</c> (409), the latter also when the unique index refuses a save
/// that won the race past the handler's own check. The migration seeds USD as the
/// base currency and AMD as a second enabled one, so both are always present without this class
/// seeding them itself.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProductTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can create a product inside an existing group and receives 201 Created.</summary>
    [Fact]
    public async Task CreateProductAsync_ReturnsCreated_AsAdminAsync()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var productResponse = await client.PostAsJsonAsync("/api/products",
            new { groupId, name = "VPS-1", description = "Entry VPS", type = ProductType.Vps, monthlyPrice = 9.99m, annualPrice = 99.99m });

        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();
        productId.Should().BeGreaterThan(0);
    }

    /// <summary>POST /api/products without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task CreateProductAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products",
            new { groupId = 1, name = "No-Auth Product", type = ProductType.SharedHosting, monthlyPrice = 5m, annualPrice = 50m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The legacy monthly/annual pair, with no per-currency list, prices the base currency alone.</summary>
    [Fact]
    public async Task CreateProduct_LegacyPairOnly_YieldsOneBaseCurrencyRow()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Starter",
            type = ProductType.SharedHosting,
            monthlyPrice = 4.99m,
            annualPrice = 49.99m,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();

        var dto = await ProductsTestSeeds.GetProductAsync(client, id);
        var prices = dto.GetProperty("prices").EnumerateArray().ToList();
        prices.Should().ContainSingle();
        prices[0].GetProperty("currencyCode").GetString().Should().Be("USD");
        prices[0].GetProperty("monthly").GetDecimal().Should().Be(4.99m);
        prices[0].GetProperty("annual").GetDecimal().Should().Be(49.99m);
    }

    /// <summary>A per-currency price list wins over the legacy pair sent alongside it.</summary>
    [Fact]
    public async Task CreateProduct_PriceListWinsOverTheLegacyPair()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Starter",
            type = ProductType.SharedHosting,
            prices = new[] { new { currencyCode = "AMD", monthly = 1200m, annual = (decimal?)null } },
            monthlyPrice = 4.99m,
            annualPrice = 49.99m,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();

        var dto = await ProductsTestSeeds.GetProductAsync(client, id);
        var prices = dto.GetProperty("prices").EnumerateArray().ToList();
        prices.Should().ContainSingle();
        prices[0].GetProperty("currencyCode").GetString().Should().Be("AMD");
        prices[0].GetProperty("monthly").GetDecimal().Should().Be(1200m);
    }

    /// <summary>Creating a product in an unknown group answers 404 with <c>PRODUCT_GROUP_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task CreateProduct_UnknownGroup_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            groupId = 999_999,
            name = "Orphan",
            type = ProductType.SharedHosting,
            monthlyPrice = 1m,
            annualPrice = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_GROUP_NOT_FOUND);
    }

    /// <summary>The featured flag and sort order sent on create are stored and returned.</summary>
    [Fact]
    public async Task CreateProduct_FeaturedAndSortOrder_AreStoredAndReturned()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Recommended",
            type = ProductType.SharedHosting,
            monthlyPrice = 2.99m,
            annualPrice = 29.99m,
            isFeatured = true,
            sortOrder = 7,
        });
        var id = await created.Content.ReadFromJsonAsync<int>();

        var dto = await ProductsTestSeeds.GetProductAsync(client, id);
        dto.GetProperty("isFeatured").GetBoolean().Should().BeTrue();
        dto.GetProperty("sortOrder").GetInt32().Should().Be(7);
    }

    /// <summary>Featuring a new product clears the flag on the group's previously featured product.</summary>
    [Fact]
    public async Task CreateProduct_Featured_ClearsTheGroupsPreviousFeaturedProduct()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);
        var first = await client.PostAsJsonAsync("/api/products", new
        {
            groupId, name = "First", type = ProductType.SharedHosting, monthlyPrice = 1m, annualPrice = 10m, isFeatured = true,
        });
        var firstId = await first.Content.ReadFromJsonAsync<int>();

        var second = await client.PostAsJsonAsync("/api/products", new
        {
            groupId, name = "Second", type = ProductType.SharedHosting, monthlyPrice = 2m, annualPrice = 20m, isFeatured = true,
        });
        var secondId = await second.Content.ReadFromJsonAsync<int>();

        (await ProductsTestSeeds.GetProductAsync(client, firstId)).GetProperty("isFeatured").GetBoolean().Should().BeFalse();
        (await ProductsTestSeeds.GetProductAsync(client, secondId)).GetProperty("isFeatured").GetBoolean().Should().BeTrue();
    }

    /// <summary>A negative sort order is refused with 400.</summary>
    [Fact]
    public async Task CreateProduct_NegativeSortOrder_IsRefused()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            groupId, name = "Bad", type = ProductType.SharedHosting, monthlyPrice = 1m, annualPrice = 10m, sortOrder = -1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A Digital product's SKU, metadata and grace period are stored and returned: the SKU normalised,
    /// the metadata the same JSON object, the type named.
    /// </summary>
    [Fact]
    public async Task CreateProduct_DigitalWithFulfilmentFields_StoresAndReturnsThem()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);
        var sku = ProductsTestSeeds.UniqueSku();

        var created = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Licence",
            type = ProductType.Digital,
            monthlyPrice = 9m,
            annualPrice = 90m,
            sku = $"  {sku}  ",
            metadata = "{ \"tier\": \"pro\", \"seats\": 5 }",
            gracePeriodDays = 30,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await ProductsTestSeeds.GetProductAsync(client, await created.Content.ReadFromJsonAsync<int>());
        dto.GetProperty("type").GetString().Should().Be(nameof(ProductType.Digital));
        dto.GetProperty("sku").GetString().Should().Be(sku.ToUpperInvariant());
        ProductsTestSeeds.AssertMetadata(dto, "{\"tier\":\"pro\",\"seats\":5}");
        dto.GetProperty("gracePeriodDays").GetInt32().Should().Be(30);
    }

    /// <summary>A product created without the fulfilment fields has no SKU or metadata and the default grace period.</summary>
    [Fact]
    public async Task CreateProduct_WithoutFulfilmentFields_HasNoSkuOrMetadataAndTheDefaultGrace()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var dto = await ProductsTestSeeds.GetProductAsync(client, await ProductsTestSeeds.CreateProductAsync(client));

        dto.GetProperty("sku").ValueKind.Should().Be(JsonValueKind.Null);
        dto.GetProperty("metadata").ValueKind.Should().Be(JsonValueKind.Null);
        dto.GetProperty("gracePeriodDays").GetInt32().Should().Be(Product.DefaultGracePeriodDays);
    }

    /// <summary>
    /// A SKU another product holds is refused with 409 <c>PRODUCT_SKU_ALREADY_IN_USE</c>, even when typed
    /// in another case, because SKUs are compared in their normalised form.
    /// </summary>
    [Fact]
    public async Task CreateProduct_SkuOfAnotherProduct_RefusesWithConflict()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);
        var sku = ProductsTestSeeds.UniqueSku();
        var first = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "First", sku));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "Second", sku.ToUpperInvariant()));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ProductsTestSeeds.AssertCodeAsync(second, HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
    }

    /// <summary>
    /// A SKU another writer takes after the handler's check but before its save is refused by the unique
    /// index, and the handler answers 409 <c>PRODUCT_SKU_ALREADY_IN_USE</c> rather than a server error,
    /// leaving only the winner's product under the SKU.
    /// </summary>
    [Fact]
    public async Task CreateProduct_SkuTakenDuringTheSave_RefusesWithConflict()
    {
        var sku = ProductsTestSeeds.UniqueSku();
        var race = new SkuRaceSaveInterceptor(sku.ToUpperInvariant());
        using var host = SkuRaceHost.Create(factory, race);
        var client = await SkuRaceHost.AdminClientAsync(factory, host);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "Loser", sku));

        race.Fired.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
        var holders = (await client.GetFromJsonAsync<JsonElement[]>("/api/products?includeUnsellable=true"))!
            .Where(p => p.GetProperty("sku").GetString() == sku.ToUpperInvariant())
            .ToList();
        holders.Should().ContainSingle();
        holders[0].GetProperty("name").GetString().Should().Be(SkuRaceSaveInterceptor.CompetitorName);
    }

    /// <summary>A malformed SKU is refused with 400 naming the field.</summary>
    [Fact]
    public async Task CreateProduct_MalformedSku_IsRefused()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "Bad", "lic pro"));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "sku");
    }

    /// <summary>Metadata that is not a JSON object is refused with 400 naming the field.</summary>
    [Fact]
    public async Task CreateProduct_MetadataThatIsNotAnObject_IsRefused()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "Bad", metadata: "[1,2]"));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "metadata");
    }

    /// <summary>Metadata over the 16 KB cap is refused with 400 naming the field.</summary>
    [Fact]
    public async Task CreateProduct_MetadataOverTheSizeCap_IsRefused()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/products", DigitalBody(groupId, "Big", metadata: ProductsTestSeeds.MetadataOverTheCap()));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "metadata");
    }

    /// <summary>A grace period outside 0 to 365 days is refused with 400 naming the field.</summary>
    /// <param name="days">A grace period just outside either bound.</param>
    [Theory]
    [InlineData(Product.MinGracePeriodDays - 1)]
    [InlineData(Product.MaxGracePeriodDays + 1)]
    public async Task CreateProduct_GracePeriodOutOfRange_IsRefused(int days)
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", DigitalBody(groupId, "Late", gracePeriodDays: days));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "gracePeriodDays");
    }

    /// <summary>The body of a Digital product create, with the fulfilment fields under test.</summary>
    /// <param name="groupId">The group the product joins.</param>
    /// <param name="name">The product name.</param>
    /// <param name="sku">The SKU to send, or null for none.</param>
    /// <param name="metadata">The metadata to send, or null for none.</param>
    /// <param name="gracePeriodDays">The grace period to send, or null for the default.</param>
    /// <returns>The anonymous body the endpoint binds.</returns>
    private static object DigitalBody(
        int groupId, string name, string? sku = null, string? metadata = null, int? gracePeriodDays = null) => new
        {
            groupId,
            name,
            type = ProductType.Digital,
            monthlyPrice = 9m,
            annualPrice = 90m,
            sku,
            metadata,
            gracePeriodDays,
        };
}
