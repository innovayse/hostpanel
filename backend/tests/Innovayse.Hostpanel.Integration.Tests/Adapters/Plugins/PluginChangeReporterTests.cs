using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginChangeReporter"/> reports a plugin change a restart applied once the host is up: a line in the activity log
/// and a mail to the admins about how it ended, each change once, however many starts follow.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginChangeReporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>An applied change is written to the activity log and mailed to the admins, then marked as reported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartedAsync_AnAppliedChange_IsLoggedAndMailedOnceAsync()
    {
        var root = PluginSigningTestSeeds.NewFolder("reporter-applied");
        try
        {
            var pluginId = $"reported-{Guid.NewGuid():N}";
            var store = NewStore(root);
            store.Record(new PluginChangeDto(pluginId, "1.0.0", "1.1.0", PluginChangeResult.Applied, null, null, TestMoments.Seeded), reported: false);
            var sender = new FakeEmailSender();
            var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

            await ReportAsync(host, store);
            await ReportAsync(host, store);

            var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail && m.Subject.Contains(pluginId)).Which;
            mail.Subject.Should().Contain("loaded and running");
            (await ActivityLinesAsync(host, pluginId)).Should().ContainSingle().Which.Should().StartWith("Plugin Change Applied By Restart");
            store.Unreported().Should().BeEmpty("the change was reported, so the next start does not report it again");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A failed change is written with its reason and the restored version, and the admins are told it failed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartedAsync_AFailedChange_IsLoggedWithTheReasonAndMailedAsFailedAsync()
    {
        var root = PluginSigningTestSeeds.NewFolder("reporter-failed");
        try
        {
            var pluginId = $"reported-{Guid.NewGuid():N}";
            var store = NewStore(root);
            store.Record(
                new PluginChangeDto(pluginId, "1.0.0", "2.0.0", PluginChangeResult.Failed, "entry point missing", "1.0.0", TestMoments.Seeded),
                reported: false);
            var sender = new FakeEmailSender();
            var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

            await ReportAsync(host, store);

            var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail && m.Subject.Contains(pluginId)).Which;
            mail.Subject.Should().Contain("change failed");
            mail.Body.Should().Contain("entry point missing");
            var line = (await ActivityLinesAsync(host, pluginId)).Should().ContainSingle().Which;
            line.Should().StartWith("Plugin Change Failed By Restart").And.Contain("Restored Version: 1.0.0").And.Contain("entry point missing");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A change that is already reported is neither logged nor mailed again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartedAsync_AReportedChange_IsLeftAloneAsync()
    {
        var root = PluginSigningTestSeeds.NewFolder("reporter-reported");
        try
        {
            var pluginId = $"reported-{Guid.NewGuid():N}";
            var store = NewStore(root);
            store.Record(new PluginChangeDto(pluginId, "1.0.0", "1.1.0", PluginChangeResult.Applied, null, null, TestMoments.Seeded), reported: true);
            var sender = new FakeEmailSender();
            var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

            await ReportAsync(host, store);

            sender.Sent.Should().BeEmpty();
            (await ActivityLinesAsync(host, pluginId)).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Builds the record of the changes over a folder.</summary>
    /// <param name="root">The upload folder.</param>
    /// <returns>The store.</returns>
    private static PluginChangeStore NewStore(string root)
    {
        return new PluginChangeStore(root, NullLogger<PluginChangeStore>.Instance);
    }

    /// <summary>Runs the reporter as the host does once it is up, and waits until the bus has handled what it published.</summary>
    /// <param name="host">The host whose database and bus the reporter uses.</param>
    /// <param name="store">The record of the changes.</param>
    /// <returns>A task that completes once the report, including the staff mail, is done.</returns>
    private static async Task ReportAsync(WebApplicationFactory<Program> host, PluginChangeStore store)
    {
        var reporter = new PluginChangeReporter(store, host.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<PluginChangeReporter>.Instance);
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => reporter.StartedAsync(CancellationToken.None)));
    }

    /// <summary>Reads the activity log lines written for a plugin.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The descriptions of the lines that name the plugin.</returns>
    private static async Task<List<string>> ActivityLinesAsync(WebApplicationFactory<Program> host, string pluginId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.Contains(pluginId))
            .Select(l => l.Description)
            .ToListAsync();
    }
}
