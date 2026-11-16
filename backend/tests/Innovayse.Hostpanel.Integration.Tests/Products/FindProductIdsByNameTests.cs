using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Queries.FindProductIdsByName;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="FindProductIdsByNameQuery"/> through the real <see cref="IMessageBus"/>: the query other modules
/// dispatch to search their rows by product name without reading the Products table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FindProductIdsByNameTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A product is found by part of its name in any letter case; a product with another name is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_PartOfTheName_ReturnsOnlyThatProductAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        int matching;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"find-{token}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);

            var product = Product.Create(group.Id, $"Gold-{token} Plan", null, null, null, $"gold-{token}", ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
            var other = Product.Create(group.Id, $"Silver-{token}", null, null, null, $"silver-{token}", ProductType.SharedHosting, 0m, 0m, TestMoments.Seeded);
            context.Products.AddRange(product, other);
            await context.SaveChangesAsync(CancellationToken.None);
            matching = product.Id;
        }

        var result = await InvokeAsync(new FindProductIdsByNameQuery($"GOLD-{token}"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(matching);
    }

    /// <summary>A percent typed into the search matches itself, so it finds no product name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_Wildcard_IsLiteralAsync()
    {
        var result = await InvokeAsync(new FindProductIdsByNameQuery("%"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>A blank text answers an empty list, not every product.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_BlankText_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new FindProductIdsByNameQuery(" "));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<int>>> InvokeAsync(FindProductIdsByNameQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<int>>>(query);
    }
}
