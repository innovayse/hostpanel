using Innovayse.Hostpanel.Application.Services.Queries.GetServiceProductIds;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="GetServiceProductIdsQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceProductIdsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A service answers its product; an unknown id adds nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownService_AnswersOnlyTheKnownProductAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, TestMoments.Seeded.AddDays(10));

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new GetServiceProductIdsQuery([seeded.ServiceId, int.MaxValue]));

        result.Value.Should().Equal(seeded.ProductId);
    }

    /// <summary>No services asked for answers no products without reading the table.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoServices_AnswersNoProductsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new GetServiceProductIdsQuery([]));

        result.Value.Should().BeEmpty();
    }
}
