using Innovayse.Hostpanel.Application.Products.Queries.GetAllowedPaymentMethods;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetAllowedPaymentMethodsQuery"/> through the real <see cref="IMessageBus"/>: the query the billing module
/// asks which payment methods the product groups of a cart allow, without reading the Products tables.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetAllowedPaymentMethodsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A product in an unrestricted group puts no restriction on the methods.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnrestrictedGroup_AnswersNoRestrictionAsync()
    {
        var productId = await ProductsTestSeeds.SeedProductAllowingPaymentMethodsAsync(factory);

        var result = await InvokeAsync(new GetAllowedPaymentMethodsQuery([productId]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    /// <summary>Two restricted groups answer the methods they share; an unknown product is ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_RestrictedGroups_AnswersTheSharedMethodsAsync()
    {
        var first = await ProductsTestSeeds.SeedProductAllowingPaymentMethodsAsync(factory, "gw-a", "gw-b");
        var second = await ProductsTestSeeds.SeedProductAllowingPaymentMethodsAsync(factory, "GW-B", "gw-c");

        var result = await InvokeAsync(new GetAllowedPaymentMethodsQuery([first, second, int.MaxValue]));

        result.Value.Should().Equal("gw-b");
    }

    /// <summary>Groups that share no method answer an empty list, not no restriction.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_GroupsSharingNothing_AnswersEmptyAsync()
    {
        var first = await ProductsTestSeeds.SeedProductAllowingPaymentMethodsAsync(factory, "gw-a");
        var second = await ProductsTestSeeds.SeedProductAllowingPaymentMethodsAsync(factory, "gw-c");

        var result = await InvokeAsync(new GetAllowedPaymentMethodsQuery([first, second]));

        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<string>?>> InvokeAsync(GetAllowedPaymentMethodsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<string>?>>(query);
    }
}
