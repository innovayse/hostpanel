using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerBriefs;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <see cref="GetServerBriefsQuery"/> through the real <see cref="IMessageBus"/>: the query the
/// Services module dispatches to show where a service runs without reading the Servers table.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServerBriefsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A known server comes back with its name, host and address; an unknown id is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_KnownAndUnknownIds_ReturnsOnlyTheKnownServerAsync()
    {
        int serverId;
        var name = $"brief-srv-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var server = Server.Create(new ServerDetails(
                Name: name,
                Hostname: "brief.example.test",
                IpAddress: "192.0.2.10",
                AssignedIpAddresses: null,
                Module: ServerModule.Cwp7,
                Username: "api",
                Credentials: new ServerCredentials("secret", null, null),
                UseSSL: true,
                MaxAccounts: null,
                IsDefault: false,
                IsDisabled: false,
                MonthlyCost: 0m,
                Datacenter: null,
                ServerStatusAddress: null,
                NameServers: ServerNameServers.None), SeededAt);
            context.Servers.Add(server);
            await context.SaveChangesAsync(CancellationToken.None);
            serverId = server.Id;
        }

        var result = await InvokeAsync(new GetServerBriefsQuery([serverId, int.MaxValue]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(
            new ServerBriefDto(serverId, name, "brief.example.test", "192.0.2.10"));
    }

    /// <summary>An empty id list answers an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoIds_ReturnsEmptyAsync()
    {
        var result = await InvokeAsync(new GetServerBriefsQuery([]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="query">The query to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<IReadOnlyList<ServerBriefDto>>> InvokeAsync(GetServerBriefsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<IReadOnlyList<ServerBriefDto>>>(query);
    }
}
