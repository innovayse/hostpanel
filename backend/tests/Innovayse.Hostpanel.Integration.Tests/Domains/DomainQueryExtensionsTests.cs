using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="DomainQueryExtensions"/> and <see cref="DomainMappingExtensions"/>, the include list
/// and the DTO mapping every "read a whole domain" use case shares, run against the real PostgreSQL host.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainQueryExtensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a domain with two nameservers and one DNS record, and returns its id.</summary>
    /// <returns>The new domain's id.</returns>
    private async Task<int> SeedDomainAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var domain = RegisteredDomain.Register(
            client.Id, $"children-{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddYears(1), autoRenew: true, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        domain.SetNameservers(["ns1.example.test", "ns2.example.test"]);
        domain.AddDnsRecord(DnsRecordType.A, "www", "192.0.2.10", 3600, null);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain.Id;
    }

    /// <summary>The shared include list brings the nameservers and DNS records along with the domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IncludeChildren_LoadsTheChildCollectionsAsync()
    {
        var domainId = await SeedDomainAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var domain = await context.Domains.IncludeChildren().FirstAsync(d => d.Id == domainId);

        domain.Nameservers.Select(n => n.Host).Should().BeEquivalentTo("ns1.example.test", "ns2.example.test");
        domain.DnsRecords.Should().ContainSingle().Which.Value.Should().Be("192.0.2.10");
    }

    /// <summary>The shared mapping carries the domain fields and every child collection into the DTO.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ToDto_MapsTheDomainAndItsChildrenAsync()
    {
        var domainId = await SeedDomainAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domain = await context.Domains.IncludeChildren().FirstAsync(d => d.Id == domainId);

        var dto = domain.ToDto();

        dto.Id.Should().Be(domainId);
        dto.Name.Should().Be(domain.Name);
        dto.Features.AutoRenew.Should().BeTrue();
        dto.Nameservers.Should().HaveCount(2);
        dto.DnsRecords.Should().ContainSingle();
        dto.EmailForwardingRules.Should().BeEmpty();
        dto.Reminders.Should().BeEmpty();
    }
}
