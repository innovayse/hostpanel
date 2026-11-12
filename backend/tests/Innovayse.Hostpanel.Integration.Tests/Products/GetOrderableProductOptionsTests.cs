using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/public/products/{id}/options</c> against Postgres: the public read the order screen
/// uses, which answers only for an active product and leaves out groups that offer nothing to choose.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetOrderableProductOptionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET is public, like the product catalogue the order screen sits beside, and answers with the groups.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheGroups_WithoutAuth()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId);
        var optionId = await ProductsTestSeeds.AddOptionAsync(admin, groupId, "Linux", 2m);

        var list = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement[]>($"/api/public/products/{productId}/options");

        var group = list!.Single();
        group.GetProperty("id").GetInt32().Should().Be(groupId);
        group.GetProperty("options")[0].GetProperty("id").GetInt32().Should().Be(optionId);
        group.GetProperty("options")[0].GetProperty("priceModifier").GetDecimal().Should().Be(2m);
    }

    /// <summary>A group with no option is left out, and the rest keep their display order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesOutGroupsWithoutOptions()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);
        var later = await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId, displayOrder: 2);
        await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId, displayOrder: 1);
        var earlier = await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId, displayOrder: 0);
        await ProductsTestSeeds.AddOptionAsync(admin, later);
        await ProductsTestSeeds.AddOptionAsync(admin, earlier);

        var list = await factory.CreateClient()
            .GetFromJsonAsync<JsonElement[]>($"/api/public/products/{productId}/options");

        list!.Select(g => g.GetProperty("id").GetInt32()).Should().Equal(earlier, later);
    }

    /// <summary>An unknown product answers 404 with <c>PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownProduct_RefusesNotFound()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/products/999999/options");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>An inactive product answers the same 404 as a missing one, so its options are not offered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InactiveProduct_RefusesNotFound()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId);
        await ProductsTestSeeds.AddOptionAsync(admin, groupId);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.Products.SingleAsync(p => p.Id == productId)).Deactivate();
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await factory.CreateClient()
            .GetAsync($"/api/public/products/{productId}/options");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A non-positive product id is refused by validation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidProductId_IsRefusedByField()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/products/0/options");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "productId");
    }
}
