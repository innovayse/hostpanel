using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Queries.GetDiskUsageByDomains;
using Innovayse.Hostpanel.Domain.Hosting;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary><see cref="GetDiskUsageByDomainsQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDiskUsageByDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The newest sample per server and domain is answered, matched without regard to case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_SeveralSamples_AnswersTheNewestPerDomainAsync()
    {
        var domain = $"usage-{Guid.NewGuid():N}.test";
        var server = $"Server-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.DiskUsageStats.Add(DiskUsageStat.Create(server, domain.ToUpperInvariant(), "Owner", 100, 1024, 10, 0, now.AddHours(-2)));
            context.DiskUsageStats.Add(DiskUsageStat.Create(server, domain, "Owner", 512, 1024, 2048, 0, now));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var rows = await AskAsync([domain]);

        var row = rows.Should().ContainSingle().Subject;
        row.ServerName.Should().Be(server);
        row.Domain.Should().Be(domain);
        row.DiskUsedMb.Should().Be(512);
        row.BandwidthUsedMb.Should().Be(2048);
    }

    /// <summary>A domain with no sample is left out and an empty list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownOrNoDomains_AnswersNothingAsync()
    {
        (await AskAsync([$"none-{Guid.NewGuid():N}.test"])).Should().BeEmpty();
        (await AskAsync([])).Should().BeEmpty();
    }

    /// <summary>Dispatches the query through the bus in its own scope.</summary>
    /// <param name="domains">The domains to look up.</param>
    /// <returns>The answered rows.</returns>
    private async Task<IReadOnlyList<DomainDiskUsageDto>> AskAsync(IReadOnlyList<string> domains)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<DomainDiskUsageDto>>>(new GetDiskUsageByDomainsQuery(domains));

        return result.Value!;
    }
}
