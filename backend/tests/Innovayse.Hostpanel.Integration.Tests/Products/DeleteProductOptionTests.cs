using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>DELETE /api/product-options/{id}</c> against Postgres: the happy path, the missing-option
/// refusal, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteProductOptionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>DELETE without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/product-options/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-delete");

        var response = await client.DeleteAsync("/api/product-options/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Deleting an unknown option id answers 404 with <c>PRODUCT_OPTION_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/product-options/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_NOT_FOUND);
    }

    /// <summary>A deleted option leaves its group, which keeps the others and the group itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovesOnlyThatOption()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);
        var removed = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Linux", 0m, 0);
        var kept = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Windows", 0m, 1);

        var response = await client.DeleteAsync($"/api/product-options/{removed}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        list!.Single().GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetInt32()).Should().Equal(kept);
    }

    /// <summary>A checkbox group accepts a new option once its only one is removed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FreesTheSlotOfASingleOptionGroup()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, ProductOptionType.Checkbox);
        var only = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Daily");

        (await client.DeleteAsync($"/api/product-options/{only}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var replacement = await ProductsTestSeeds.AddOptionAsync(client, groupId, "Weekly");
        replacement.Should().BeGreaterThan(0);
    }
}
