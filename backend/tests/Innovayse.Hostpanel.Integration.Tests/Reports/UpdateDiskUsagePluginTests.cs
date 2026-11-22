using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Monitoring;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// The disk usage refresh for a server a panel plugin serves: the accounts are listed page by page, each account's usage is read
/// (bytes become megabytes, a limit the panel does not report becomes the report's unlimited marker), and a plugin that cannot
/// list accounts, is off or throws is a failure line, not a missing server.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDiskUsagePluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every page of the listing is read and every account's usage is stored in megabytes under the server's name.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateNow_PagedListing_StoresEveryAccountsUsageAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.List = cursor => cursor is null
            ? PanelAccountListResult.Page(
                [Account("one"), Account("two")], "page-2")
            : PanelAccountListResult.Page([Account("three")], null);

        var report = await Service(harness).UpdateNowAsync(CancellationToken.None);

        report.Failures.Should().NotContain(f => f.ServerName == harness.Server.Name);
        var stored = await harness.Db.DiskUsageStats.AsNoTracking().Where(s => s.ServerName == harness.Server.Name).OrderBy(s => s.Domain).ToListAsync();
        stored.Select(s => s.Domain).Should().Equal("one.example.test", "three.example.test", "two.example.test");
        stored.Should().OnlyContain(s => s.DiskUsageMb == 10 && s.DiskLimitMb == 100 && s.BwUsageMb == 5 && s.BwLimitMb == 0);
        harness.Script.Received.OfType<PanelListAccountsContext>().Select(c => c.Cursor).Should().Equal(new string?[] { null, "page-2" });
        harness.Script.Received.OfType<PanelAccountContext>().Select(c => c.Service.ProvisioningRef).Should().BeEquivalentTo("one", "two", "three");
    }

    /// <summary>A panel that cannot list its accounts is an unsupported module line, with nothing stored.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateNow_ListingNotSupported_IsAnUnsupportedModuleFailureAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.List = _ => PanelAccountListResult.NotSupported();

        var report = await Service(harness).UpdateNowAsync(CancellationToken.None);

        report.Failures.Should().Contain(new DiskUsageFailureDto(harness.Server.Name, DiskUsageFailureReason.UnsupportedModule));
        (await harness.Db.DiskUsageStats.AsNoTracking().AnyAsync(s => s.ServerName == harness.Server.Name)).Should().BeFalse();
    }

    /// <summary>A switched off plugin and a throwing plugin are unreachable lines, never an exception out of the refresh.</summary>
    /// <param name="enabled">Whether the plugin is switched on.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateNow_OffOrThrowingPlugin_IsAnUnreachableFailureAsync(bool enabled)
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled);
        harness.Script.Throw = new InvalidOperationException("plugin internal text");

        var report = await Service(harness).UpdateNowAsync(CancellationToken.None);

        report.Failures.Should().Contain(new DiskUsageFailureDto(harness.Server.Name, DiskUsageFailureReason.Unreachable));
    }

    /// <summary>A listing that never ends is cut at the page limit and reported as a failure instead of holding the refresh forever.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpdateNow_EndlessListing_IsCutAtThePageLimitAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        harness.Script.List = cursor => PanelAccountListResult.Page([], $"after-{cursor}");

        var report = await Service(harness).UpdateNowAsync(CancellationToken.None);

        report.Failures.Should().Contain(new DiskUsageFailureDto(harness.Server.Name, DiskUsageFailureReason.Unreachable));
        harness.Script.Received.OfType<PanelListAccountsContext>().Should().HaveCount(1000);
    }

    /// <summary>Builds one listed account that has a domain.</summary>
    /// <param name="reference">The account's reference; its domain is derived from it.</param>
    /// <returns>The account.</returns>
    private static PanelAccount Account(string reference)
    {
        return new PanelAccount(reference, $"{reference}.example.test", PanelAccountStatus.Active, "basic", null);
    }

    /// <summary>Builds the disk usage service over the harness's real plugin parts.</summary>
    /// <param name="harness">The installed plugin.</param>
    /// <returns>The service.</returns>
    private static DiskUsageService Service(PanelPluginHarness harness)
    {
        return new DiskUsageService(
            harness.Db,
            harness.Service<IHttpClientFactory>(),
            NullLogger<DiskUsageService>.Instance,
            harness.Service<IClock>(),
            harness.Resolver,
            new PanelPluginAccountReader(harness.Caller));
    }
}
