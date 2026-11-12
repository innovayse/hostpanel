using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>DELETE /api/product-option-groups/{id}</c> against Postgres: the happy path, the missing-group
/// refusal, the authorization boundary, and the cascade from the product.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteProductOptionGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>DELETE without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/product-option-groups/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "option-group-delete");

        var response = await client.DeleteAsync("/api/product-option-groups/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Deleting an unknown group id answers 404 with <c>PRODUCT_OPTION_GROUP_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/product-option-groups/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_OPTION_GROUP_NOT_FOUND);
    }

    /// <summary>A deleted group leaves the list and takes its options with it, but not its sibling groups.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovesTheGroupAndItsOptions()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);
        var optionId = await ProductsTestSeeds.AddOptionAsync(client, id);
        var sibling = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId, displayOrder: 1);

        var deleted = await client.DeleteAsync($"/api/product-option-groups/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-option-groups?productId={productId}");
        list!.Select(g => g.GetProperty("id").GetInt32()).Should().Equal(sibling);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ProductOptionGroups.SelectMany(g => g.Options).AnyAsync(o => o.Id == optionId)).Should().BeFalse();
    }

    /// <summary>Removing a product removes its groups and their options, which describe nothing without it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeletingTheProduct_RemovesItsGroupsAndOptions()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var groupId = await ProductsTestSeeds.CreateOptionGroupAsync(client, productId);
        var optionId = await ProductsTestSeeds.AddOptionAsync(client, groupId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Products.Remove(await context.Products.SingleAsync(p => p.Id == productId));
        await context.SaveChangesAsync(CancellationToken.None);

        using var verify = factory.Services.CreateScope();
        var fresh = verify.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await fresh.ProductOptionGroups.AnyAsync(g => g.Id == groupId)).Should().BeFalse();
        (await fresh.ProductOptionGroups.SelectMany(g => g.Options).AnyAsync(o => o.Id == optionId)).Should().BeFalse();
    }
}
