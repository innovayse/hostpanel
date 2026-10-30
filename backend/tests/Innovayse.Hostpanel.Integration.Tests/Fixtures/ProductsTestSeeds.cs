using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Constants;

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
    /// <returns>The new product's id.</returns>
    public static async Task<int> CreateProductAsync(HttpClient client)
    {
        var groupId = await CreateGroupAsync(client);

        var product = await client.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Featured",
            type = ProductType.SharedHosting,
            monthlyPrice = 1m,
            annualPrice = 10m,
        });
        return await product.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Reads back a created product's admin-grid view, including unsellable rows.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="productId">The product to find.</param>
    /// <returns>The product's JSON element from the admin list.</returns>
    public static async Task<JsonElement> GetProductAsync(HttpClient client, int productId)
    {
        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/products?includeUnsellable=true");
        return list!.Single(p => p.GetProperty("id").GetInt32() == productId);
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
