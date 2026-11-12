using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/products/bundles</c> against Postgres: the happy path, the bundle rules the aggregate
/// enforces and the refusals the handler adds because they span aggregates.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProductBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin creates a bundle and reads back every field and its components, the primary one first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_AsAdmin_RoundTripsFieldsAndComponents()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var hosting = await ProductsTestSeeds.CreateProductAsync(client);
        var vps = await ProductsTestSeeds.CreateProductAsync(client, ProductType.Vps);
        var body = ProductsTestSeeds.BundleBody(hosting, vps);
        body["discountType"] = "Fixed";
        body["discountAmount"] = 25m;
        body["discountCurrency"] = "usd";
        body["domainYears"] = 3;

        var id = await ProductsTestSeeds.CreateBundleAsync(client, body);

        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("name").GetString().Should().Be((string)body["name"]!);
        bundle.GetProperty("isActive").GetBoolean().Should().BeTrue();
        bundle.GetProperty("includesDomain").GetBoolean().Should().BeTrue();
        bundle.GetProperty("domainYears").GetInt32().Should().Be(3);
        bundle.GetProperty("discountType").GetString().Should().Be("Fixed");
        bundle.GetProperty("discountAmount").GetDecimal().Should().Be(25m);
        bundle.GetProperty("discountCurrency").GetString().Should().Be("USD");
        var components = bundle.GetProperty("components").EnumerateArray().ToList();
        components.Select(c => c.GetProperty("productId").GetInt32()).Should().Equal(hosting, vps);
        components[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        components[1].GetProperty("isPrimary").GetBoolean().Should().BeFalse();
    }

    /// <summary>A bundle without a domain is created with no years.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutADomain_HasNoYears()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(await ProductsTestSeeds.CreateProductAsync(client));
        body["includesDomain"] = false;
        body["domainYears"] = null;
        body["discountType"] = "None";
        body["discountAmount"] = 0m;

        var id = await ProductsTestSeeds.CreateBundleAsync(client, body);

        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("domainYears").ValueKind.Should().Be(JsonValueKind.Null);
        bundle.GetProperty("discountType").GetString().Should().Be("None");
    }

    /// <summary>POST /api/products/bundles without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/products/bundles", ProductsTestSeeds.BundleBody(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A component naming no product answers 404 with <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnknownProduct_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/products/bundles", ProductsTestSeeds.BundleBody(999999));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_PRODUCT_NOT_FOUND);
    }

    /// <summary>A domain product cannot be a component: the domain is the bundle's own flag.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_DomainProduct_Refuses()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var hosting = await ProductsTestSeeds.CreateProductAsync(client);
        var domain = await ProductsTestSeeds.CreateProductAsync(client, ProductType.Domain);

        var response = await client.PostAsJsonAsync("/api/products/bundles", ProductsTestSeeds.BundleBody(hosting, domain));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED);
    }

    /// <summary>The same product listed twice answers 400 with <c>PRODUCT_BUNDLE_ITEM_DUPLICATE</c>, not "not found".</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_DuplicateProduct_Refuses()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var hosting = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PostAsJsonAsync("/api/products/bundles", ProductsTestSeeds.BundleBody(hosting, hosting));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_ITEM_DUPLICATE);
    }

    /// <summary>Two primary components answer 400 with <c>PRODUCT_BUNDLE_PRIMARY_INVALID</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_TwoPrimaries_Refuses()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var first = await ProductsTestSeeds.CreateProductAsync(client);
        var second = await ProductsTestSeeds.CreateProductAsync(client);
        var body = ProductsTestSeeds.BundleBody(first, second);
        body["items"] = new[] { new { productId = first, isPrimary = true }, new { productId = second, isPrimary = true } };

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_PRIMARY_INVALID);
    }

    /// <summary>A bundle with no component is refused as a field error on the items.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_NoItems_RefusesTheItemsField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(1);
        body["items"] = Array.Empty<object>();

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "items");
    }

    /// <summary>A blank name is refused as a field error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_BlankName_RefusesTheNameField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(1);
        body["name"] = "";

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "name");
    }

    /// <summary>A percentage above 100 is refused as a field error on the amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_PercentageOver100_RefusesTheAmountField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(1);
        body["discountAmount"] = 100.5m;

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "discountAmount");
    }

    /// <summary>A fixed discount without a currency is refused as a field error on the amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_FixedWithoutCurrency_RefusesTheAmountField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(1);
        body["discountType"] = "Fixed";

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "discountAmount");
    }

    /// <summary>A domain bundle with 11 years is refused as a field error on the years.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ElevenYears_RefusesTheYearsField()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var body = ProductsTestSeeds.BundleBody(1);
        body["domainYears"] = 11;

        var response = await client.PostAsJsonAsync("/api/products/bundles", body);

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "domainYears");
    }
}
