using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/restart</c> (Restart now): an administrator starts the host's graceful restart and the request
/// is written to the activity log first; a caller with only the Client role is refused with 403, an anonymous caller with 401,
/// and neither starts anything. A plugin install that the running process cannot apply in place starts the same restart by
/// itself, and the answer says so. The real restart is replaced by a recording one, because it would stop the shared test host.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RestartPluginHostTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The restarter the test host uses, which counts the restarts it was asked for.</summary>
    private RecordingHostRestarter Restarter => (RecordingHostRestarter)factory.Services.GetRequiredService<IHostRestarter>();

    /// <summary>An administrator's restart answers 202, starts one restart and is audited before it starts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Restart_AsAnAdministrator_StartsTheRestartAndWritesTheAuditLineAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var before = Restarter.Restarts;

        var response = await client.PostAsync("/api/admin/plugins/restart", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Restarter.Restarts.Should().Be(before + 1);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var line = await db.ActivityLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .FirstAsync(l => l.Description == "Restarted Plugin Host");
        line.ClientId.Should().BeNull("a host restart concerns no client");
    }

    /// <summary>A caller with only the Client role is refused with 403 and nothing restarts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Restart_AsTheClientRole_IsRefusedAndStartsNothingAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "restart-plugin-host-forbidden");
        var before = Restarter.Restarts;

        var response = await client.PostAsync("/api/admin/plugins/restart", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Restarter.Restarts.Should().Be(before);
    }

    /// <summary>An anonymous caller is refused with 401 and nothing restarts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Restart_Anonymously_IsRefusedAndStartsNothingAsync()
    {
        var before = Restarter.Restarts;

        var response = await factory.CreateClient().PostAsync("/api/admin/plugins/restart", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Restarter.Restarts.Should().Be(before);
    }

    /// <summary>
    /// Installing a plugin that was never loaded cannot be applied in the running process, so the install itself starts the
    /// restart, answers that it is restarting and writes the apply to the activity log.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Install_APluginThatCannotBeReloaded_StartsTheRestartByItselfAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var before = Restarter.Restarts;

        var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
        body.GetProperty("restarting").GetBoolean().Should().BeTrue();
        body.GetProperty("reloaded").GetBoolean().Should().BeFalse();
        Restarter.Restarts.Should().BeGreaterThan(before);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().AnyAsync(l =>
            l.Description.StartsWith("Plugin Change Applying By Restart") && l.Description.Contains(pluginId))).Should().BeTrue();
    }
}
