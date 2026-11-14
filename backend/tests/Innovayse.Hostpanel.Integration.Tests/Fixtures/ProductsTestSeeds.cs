using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and authentication helpers shared by every Products use-case test file, so the seeding
/// logic exists exactly once rather than once per use case.
/// </summary>
public static class ProductsTestSeeds
{
    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The factory whose admin credentials to mint a token from.</param>
    /// <returns>An authenticated client.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates a product group through the API and returns its id.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <returns>The new group's id.</returns>
    public static async Task<int> CreateGroupAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/products/groups", new { name = $"group-{Guid.NewGuid():N}" });
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Creates a product (in a fresh group) through the API and returns its id.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="type">The product type; shared hosting unless a test needs another.</param>
    /// <returns>The new product's id.</returns>
    public static async Task<int> CreateProductAsync(HttpClient client, ProductType type = ProductType.SharedHosting)
    {
        var groupId = await CreateGroupAsync(client);

        var product = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Featured",
            type,
            monthlyPrice = 1m,
            annualPrice = 10m,
        });
        return await product.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>
    /// A valid create or update body for a product bundle with a domain and a 10 percent discount. The first
    /// product is the primary component. A test overrides the keys it exercises.
    /// </summary>
    /// <param name="productIds">The component products; at least one.</param>
    /// <returns>The body as a dictionary, serialised with the property names the API reads.</returns>
    public static Dictionary<string, object?> BundleBody(params int[] productIds)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = $"bundle-{Guid.NewGuid():N}",
            ["description"] = "Hosting with a domain",
            ["sortOrder"] = 0,
            ["includesDomain"] = true,
            ["domainYears"] = 1,
            ["discountType"] = "Percentage",
            ["discountAmount"] = 10m,
            ["discountCurrency"] = null,
            ["items"] = productIds.Select((id, index) => new { productId = id, isPrimary = index == 0 }).ToArray(),
        };
    }

    /// <summary>Creates a product bundle through the API and returns its id.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="body">The create body, from <see cref="BundleBody"/>.</param>
    /// <returns>The new bundle's id.</returns>
    public static async Task<int> CreateBundleAsync(HttpClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostAsJsonAsync("/api/products/bundles", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Stores a product in a fresh group with exactly the given USD prices, one per cycle, and no others.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="prices">The cycles to price and their USD amounts.</param>
    /// <returns>The new product id.</returns>
    public static async Task<int> SeedProductWithUsdPricesAsync(IntegrationTestFactory factory, params (BillingCycle Cycle, decimal Amount)[] prices)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ProductGroup.Create($"priced-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Priced", null, null, null, null, ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
        foreach (var (cycle, amount) in prices)
        {
            product.SetPrice("USD", cycle, amount);
        }

        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        return product.Id;
    }

    /// <summary>Reads back a created product's admin-grid view, including unsellable and inactive rows.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="productId">The product to find.</param>
    /// <returns>The product's JSON element from the admin list.</returns>
    public static async Task<JsonElement> GetProductAsync(HttpClient client, int productId)
    {
        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/products?activeOnly=false&includeUnsellable=true");
        return list!.Single(p => p.GetProperty("id").GetInt32() == productId);
    }

    /// <summary>Reads the ids the anonymous storefront catalogue lists for a group.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="groupId">The group whose catalogue is read.</param>
    /// <returns>The ids of the products the storefront shows.</returns>
    public static async Task<int[]> PublicProductIdsAsync(IntegrationTestFactory factory, int groupId)
    {
        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>($"/api/public/products?groupId={groupId}");
        return list!.Select(p => p.GetProperty("id").GetInt32()).ToArray();
    }

    /// <summary>Creates a product option group through the API and returns its id.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="productId">The product the group configures.</param>
    /// <param name="type">The group type; sent as its name, the way the wire spells it.</param>
    /// <param name="displayOrder">Position within the product.</param>
    /// <param name="minQuantity">Least amount, for a quantity group.</param>
    /// <param name="maxQuantity">Most amount, for a quantity group.</param>
    /// <returns>The new group's id.</returns>
    public static async Task<int> CreateOptionGroupAsync(
        HttpClient client,
        int productId,
        ProductOptionType type = ProductOptionType.Dropdown,
        int displayOrder = 0,
        int? minQuantity = null,
        int? maxQuantity = null)
    {
        var response = await client.PostAsJsonAsync("/api/product-option-groups", new
        {
            productId,
            name = $"group-{Guid.NewGuid():N}",
            type = type.ToString(),
            isRequired = false,
            displayOrder,
            minQuantity,
            maxQuantity,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Adds an option to a group through the API and returns its id.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="groupId">The group the option belongs to.</param>
    /// <param name="name">The option name.</param>
    /// <param name="priceModifier">The recurring modifier.</param>
    /// <param name="displayOrder">Position within the group.</param>
    /// <returns>The new option's id.</returns>
    public static async Task<int> AddOptionAsync(
        HttpClient client, int groupId, string name = "Option", decimal priceModifier = 0m, int displayOrder = 0)
    {
        var response = await client.PostAsJsonAsync("/api/product-options", new
        {
            groupId,
            name,
            priceModifier,
            priceType = nameof(ProductOptionPriceType.Fixed),
            setupFeeModifier = 0m,
            displayOrder,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>A SKU no other test uses, in lower case so the tests also see it normalised.</summary>
    /// <returns>The SKU as a caller would type it.</returns>
    public static string UniqueSku() => $"lic-{Guid.NewGuid():N}";

    /// <summary>A JSON object whose text is exactly <paramref name="bytes"/> bytes of UTF-8.</summary>
    /// <param name="bytes">The size wanted, at least the size of the empty wrapper object.</param>
    /// <returns>The metadata text.</returns>
    public static string MetadataOfBytes(int bytes)
    {
        const string Prefix = "{\"blob\":\"";
        const string Suffix = "\"}";
        var metadata = Prefix + new string('a', bytes - Prefix.Length - Suffix.Length) + Suffix;
        Encoding.UTF8.GetByteCount(metadata).Should().Be(bytes);
        return metadata;
    }

    /// <summary>
    /// Asserts a product DTO's metadata is the same JSON as <paramref name="expected"/>. Compared as JSON,
    /// not as text: the <c>jsonb</c> column hands the bag back in the database's own spacing.
    /// </summary>
    /// <param name="dto">The product's JSON element from the admin list.</param>
    /// <param name="expected">The JSON object the metadata must equal.</param>
    public static void AssertMetadata(JsonElement dto, string expected)
    {
        var actual = JsonNode.Parse(dto.GetProperty("metadata").GetString()!);
        JsonNode.DeepEquals(actual, JsonNode.Parse(expected)).Should().BeTrue(
            "the stored metadata {0} should equal {1}", actual?.ToJsonString(), expected);
    }

    /// <summary>A JSON object one byte over <see cref="Product.MaxMetadataBytes"/>.</summary>
    /// <returns>The metadata text.</returns>
    public static string MetadataOverTheCap() => MetadataOfBytes(Product.MaxMetadataBytes + 1);

    /// <summary>
    /// Asserts the response is the validation refusal (400 <c>VALIDATION_ERROR</c>) and that it names
    /// <paramref name="field"/>, compared without regard to case because the wire is camelCase.
    /// </summary>
    /// <param name="response">The refusal.</param>
    /// <param name="field">The command property the refusal must name.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertFieldRefusedAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.ValidationError);
        body.RootElement.GetProperty("errors").EnumerateObject()
            .Select(e => e.Name.ToLowerInvariant())
            .Should().Contain(field.ToLowerInvariant());
    }

    /// <summary>Asserts the refusal carries the expected error code, which is what a client branches on.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }
}
