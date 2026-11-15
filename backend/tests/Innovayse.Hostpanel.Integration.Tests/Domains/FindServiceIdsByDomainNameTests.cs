using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Queries.FindServiceIdsByDomainName;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="FindServiceIdsByDomainNameQuery"/> through the real <see cref="IMessageBus"/>: the query the Services
/// module dispatches to search its list by a service's registered domain without reading the Domains table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FindServiceIdsByDomainNameTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A service is found by any of its linked domains, in any letter case, once; an unlinked domain with the same
    /// text and a service with another domain are left out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_TextInAnyLinkedDomain_ReturnsTheServiceOnceAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        int linkedService;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            linkedService = await DomainsTestSeeds.SeedServiceAsync(context, clientId);
            var otherService = await DomainsTestSeeds.SeedServiceAsync(context, clientId);
            AddDomain(context, clientId, $"first-{token}.example", linkedService);
            AddDomain(context, clientId, $"second-{token}.example", linkedService);
            AddDomain(context, clientId, $"elsewhere-{Guid.NewGuid():N}.example", otherService);
            AddDomain(context, clientId, $"unlinked-{token}.example", serviceId: null);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var byFirst = await InvokeAsync(new FindServiceIdsByDomainNameQuery($"FIRST-{token}"));
        var byBoth = await InvokeAsync(new FindServiceIdsByDomainNameQuery($"-{token}.example"));
        var byUnlinked = await InvokeAsync(new FindServiceIdsByDomainNameQuery($"unlinked-{token}"));

        byFirst.Value.Should().Equal(linkedService);
        byBoth.Value.Should().Equal(linkedService);
        byUnlinked.Value.Should().BeEmpty();
    }

    /// <summary>A percent typed into the search matches itself, so it finds no domain name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_Wildcard_IsLiteralAsync()
    {
        var result = await InvokeAsync(new FindServiceIdsByDomainNameQuery("%"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>A blank text answers an empty list, not every linked service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_BlankText_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new FindServiceIdsByDomainNameQuery("  "));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Stages a registered domain, linked to a service when one is given.</summary>
    /// <param name="context">The context to stage it on.</param>
    /// <param name="clientId">The owner of the domain.</param>
    /// <param name="name">The domain name.</param>
    /// <param name="serviceId">The service to link, or <see langword="null"/> for none.</param>
    private static void AddDomain(IHostpanelDbContext context, int clientId, string name, int? serviceId)
    {
        var domain = RegisteredDomain.Register(
            clientId, name, TestMoments.Seeded, TestMoments.Seeded.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        if (serviceId is { } id)
        {
            domain.LinkService(id);
        }

        context.Domains.Add(domain);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<int>>> InvokeAsync(FindServiceIdsByDomainNameQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<int>>>(query);
    }
}
