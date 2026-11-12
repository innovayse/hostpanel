using Innovayse.Hostpanel.Application.Admin.Servers.Commands.SyncServerAccountCountsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// Integration tests for <see cref="SyncServerAccountCountsCronHandler"/>: it tests connectivity
/// to each non-disabled server and records the result, skipping disabled servers, and never
/// throws past a single server's own failure.
/// </summary>
/// <remarks>
/// A cron under <c>Commands/</c>, dispatched through the real <see cref="IMessageBus"/> the way
/// <c>AutoTerminateSuspendedCronTests</c> does. The one port this platform does not own
/// (<see cref="IServerConnectionTester"/>, a real network call to the hosting server) is
/// replaced by <see cref="FakeServerConnectionTester"/>.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncServerAccountCountsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose server-connection tester is <paramref name="tester"/>.</summary>
    /// <param name="tester">The fake tester the host should resolve instead of a real one.</param>
    /// <returns>The isolated host.</returns>
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWithFakeTester(
        FakeServerConnectionTester tester) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IServerConnectionTester>(tester)));

    /// <summary>Seeds a non-disabled server directly against the database.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="isDisabled">Whether the seeded server should be disabled.</param>
    /// <returns>The seeded server.</returns>
    private static async Task<Server> SeedServerAsync(IHostpanelDbContext context, bool isDisabled)
    {
        var server = Server.Create(new ServerDetails(
            Name: $"sync-srv-{Guid.NewGuid():N}",
            Hostname: "sync.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.Cwp7,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, null),
            UseSSL: true,
            MaxAccounts: null,
            IsDefault: false,
            IsDisabled: isDisabled,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), TestMoments.Seeded);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);
        return server;
    }

    /// <summary>A non-disabled server is tested and its connection result recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NonDisabledServer_IsTestedAndItsConnectionResultRecorded()
    {
        var tester = new FakeServerConnectionTester { AccountsCount = 7 };
        var host = HostWithFakeTester(tester);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = await SeedServerAsync(context, isDisabled: false);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new SyncServerAccountCountsCronCommand(), CancellationToken.None);

        tester.TestedServers.Should().ContainSingle(s => s.Id == server.Id);
        var reloaded = await context.Servers.AsNoTracking().FirstAsync(s => s.Id == server.Id, CancellationToken.None);
        reloaded.AccountsCount.Should().Be(7);
    }

    /// <summary>A disabled server is skipped entirely: never handed to the connection tester.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledServer_IsSkippedEntirely()
    {
        var tester = new FakeServerConnectionTester();
        var host = HostWithFakeTester(tester);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = await SeedServerAsync(context, isDisabled: true);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new SyncServerAccountCountsCronCommand(), CancellationToken.None);

        tester.TestedServers.Should().NotContain(s => s.Id == server.Id);
    }

    /// <summary>A server whose connection test fails is still recorded, not left to crash the run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServerWhoseConnectionTestFails_IsRecordedNotCrashed()
    {
        var tester = new FakeServerConnectionTester { ShouldFail = true };
        var host = HostWithFakeTester(tester);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = await SeedServerAsync(context, isDisabled: false);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var act = async () => await bus.InvokeAsync(new SyncServerAccountCountsCronCommand(), CancellationToken.None);

        await act.Should().NotThrowAsync();
        var reloaded = await context.Servers.AsNoTracking().FirstAsync(s => s.Id == server.Id, CancellationToken.None);
        reloaded.AccountsCount.Should().BeNull();
    }

    /// <summary>
    /// One server whose result cannot be saved is skipped and does not poison the next server's save:
    /// the following server still gets its account count recorded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AServerWhoseResultCannotBeSaved_DoesNotStopTheNext()
    {
        var tester = new FakeServerConnectionTester { AccountsCount = 9 };
        var poisonedId = 0;
        using var failingHost = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(TestMoments.Seeded),
            tracker => tracker.Entries<Server>().Any(e => e.State == EntityState.Modified && e.Entity.Id == poisonedId));
        using var host = failingHost.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IServerConnectionTester>(tester)));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = await SeedServerAsync(context, isDisabled: false);
        var other = await SeedServerAsync(context, isDisabled: false);
        poisonedId = failing.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SyncServerAccountCountsCronCommand(), CancellationToken.None);

        var failingAfter = await context.Servers.AsNoTracking().FirstAsync(s => s.Id == failing.Id, CancellationToken.None);
        var otherAfter = await context.Servers.AsNoTracking().FirstAsync(s => s.Id == other.Id, CancellationToken.None);
        failingAfter.AccountsCount.Should().BeNull();
        otherAfter.AccountsCount.Should().Be(9);
    }
}
