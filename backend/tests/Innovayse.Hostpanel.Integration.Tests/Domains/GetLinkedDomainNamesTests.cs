using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetLinkedDomainNames;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="GetLinkedDomainNamesQuery"/> through the real <see cref="IMessageBus"/>: the query the
/// Services module dispatches to show a service's registered domain without reading the Domains table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetLinkedDomainNamesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A linked service gets its domain's name; a service with no linked domain is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_LinkedAndUnlinkedServices_ReturnsOnlyTheLinkedOneAsync()
    {
        int serviceId;
        var name = $"linked-{Guid.NewGuid():N}"[..19] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            serviceId = await DomainsTestSeeds.SeedServiceAsync(context, clientId);
            var domain = RegisteredDomain.Register(
                clientId, name, TestMoments.Seeded, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            domain.LinkService(serviceId);
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await InvokeAsync(new GetLinkedDomainNamesQuery([serviceId, int.MaxValue]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(new LinkedDomainNameDto(serviceId, name));
    }

    /// <summary>An empty id list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new GetLinkedDomainNamesQuery([]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<LinkedDomainNameDto>>> InvokeAsync(GetLinkedDomainNamesQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<LinkedDomainNameDto>>>(query);
    }
}
