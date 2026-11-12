using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Commands.CheckStatusComponentsCron;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for <see cref="CheckStatusComponentsCronHandler"/>: it requests the address of every monitored
/// component, records the outcome, opens an incident after as many failures in a row as the component's threshold, closes
/// it on recovery, leaves a forced state alone and deletes the records older than the retention.
/// </summary>
/// <remarks>
/// A cron under <c>Commands/</c>, dispatched through the real <see cref="IMessageBus"/>. The one port this platform does
/// not own (<see cref="IStatusProbe"/>, a real request to a component's address) is replaced by <see cref="FakeStatusProbe"/>,
/// and the clock is one the test moves by hand.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckStatusComponentsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose probe is <paramref name="probe"/> and whose clock is <paramref name="clock"/>.</summary>
    /// <param name="probe">The fake probe the host should resolve instead of a real one.</param>
    /// <param name="clock">The clock the host's handlers read.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> HostWith(FakeStatusProbe probe, ManualTimeProvider clock) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IStatusProbe>(probe);
            }));

    /// <summary>Seeds a component in a group of its own, directly against the database.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="url">The address the component is checked by; unique to the test.</param>
    /// <param name="threshold">How many failed checks in a row make the component count as down.</param>
    /// <param name="enabled">Whether the automatic check runs.</param>
    /// <returns>The id of the new component.</returns>
    private static async Task<int> SeedComponentAsync(WebApplicationFactory<Program> host, string url, int threshold, bool enabled)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = StatusComponentGroup.Create($"Group {Guid.NewGuid():N}", 0);
        var component = group.AddComponent("Web hosting", null, 0);
        component.ConfigureMonitoring(url, enabled, threshold);
        context.StatusComponentGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        return component.Id;
    }

    /// <summary>Runs one pass of the job.</summary>
    /// <param name="host">The host to run it on.</param>
    /// <returns>A task that completes when the pass has run.</returns>
    private static async Task RunPassAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new CheckStatusComponentsCronCommand(), CancellationToken.None);
    }

    /// <summary>Reads a component as it is stored.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="id">The component id.</param>
    /// <returns>The stored component.</returns>
    private static async Task<StatusComponent> ReadComponentAsync(WebApplicationFactory<Program> host, int id)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .StatusComponents.AsNoTracking().FirstAsync(c => c.Id == id, CancellationToken.None);
    }

    /// <summary>Reads an incident as it is stored.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="id">The incident id.</param>
    /// <returns>The stored incident, with its timeline.</returns>
    private static async Task<StatusIncident> ReadIncidentAsync(WebApplicationFactory<Program> host, int id)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .StatusIncidents.AsNoTracking().FirstAsync(i => i.Id == id, CancellationToken.None);
    }

    /// <summary>A pass requests the address, records the check and leaves a healthy component operational.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_RecordsTheCheck_AndKeepsAHealthyComponentOperational()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 2, enabled: true);

        await RunPassAsync(host);

        probe.Requested.Should().Contain(url);
        using var scope = host.Services.CreateScope();
        var checks = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .StatusChecks.AsNoTracking().Where(c => c.ComponentId == id).ToListAsync(CancellationToken.None);
        checks.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        (await ReadComponentAsync(host, id)).CurrentState.Should().Be(StatusComponentState.Operational);
    }

    /// <summary>A component whose check is switched off is never requested.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_SkipsAComponentWhoseMonitoringIsOff()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        await SeedComponentAsync(host, url, 2, enabled: false);

        await RunPassAsync(host);

        probe.Requested.Should().NotContain(url);
    }

    /// <summary>
    /// Failures below the threshold open nothing; the failure that reaches it makes the component an outage and opens an
    /// incident that names the component, once, however many passes follow.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_OpensOneIncident_WhenFailuresReachTheThreshold()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 2, enabled: true);
        probe.Fail(url);

        await RunPassAsync(host);
        var afterOne = await ReadComponentAsync(host, id);
        afterOne.CurrentState.Should().Be(StatusComponentState.Operational);
        afterOne.ActiveIncidentId.Should().BeNull();

        await RunPassAsync(host);
        var afterTwo = await ReadComponentAsync(host, id);
        afterTwo.CurrentState.Should().Be(StatusComponentState.Outage);
        afterTwo.ActiveIncidentId.Should().NotBeNull();
        var incident = await ReadIncidentAsync(host, afterTwo.ActiveIncidentId!.Value);
        incident.State.Should().Be(StatusIncidentState.Investigating);
        incident.Title.Should().Contain("Web hosting");

        await RunPassAsync(host);
        (await ReadComponentAsync(host, id)).ActiveIncidentId.Should().Be(afterTwo.ActiveIncidentId);
    }

    /// <summary>A success after the outage resolves the incident the job opened and makes the component operational again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_ResolvesTheIncident_WhenTheComponentRecovers()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 1, enabled: true);
        probe.Fail(url);
        await RunPassAsync(host);
        var incidentId = (await ReadComponentAsync(host, id)).ActiveIncidentId!.Value;

        probe.Recover(url);
        await RunPassAsync(host);

        var component = await ReadComponentAsync(host, id);
        component.CurrentState.Should().Be(StatusComponentState.Operational);
        component.ActiveIncidentId.Should().BeNull();
        var incident = await ReadIncidentAsync(host, incidentId);
        incident.State.Should().Be(StatusIncidentState.Resolved);
        incident.ResolvedAt.Should().NotBeNull();
    }

    /// <summary>
    /// An incident an operator closed by hand is not updated again: the pass only drops the link and leaves the incident as it is.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_LeavesAnIncidentTheOperatorClosed_AndDropsTheLink()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 1, enabled: true);
        probe.Fail(url);
        await RunPassAsync(host);
        var incidentId = (await ReadComponentAsync(host, id)).ActiveIncidentId!.Value;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var open = await context.StatusIncidents.FirstAsync(i => i.Id == incidentId, CancellationToken.None);
            open.PostUpdate(StatusIncidentState.Resolved, "Closed by hand.", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        probe.Recover(url);
        await RunPassAsync(host);

        (await ReadComponentAsync(host, id)).ActiveIncidentId.Should().BeNull();
        var incident = await ReadIncidentAsync(host, incidentId);
        incident.Updates.Should().HaveCount(2);
    }

    /// <summary>
    /// A state an operator forces stays on the page while the checks fail, though the incident is still opened; clearing the
    /// override shows what the checks found.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_KeepsAForcedState_WhileTheChecksFail()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 1, enabled: true);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var forced = await context.StatusComponents.FirstAsync(c => c.Id == id, CancellationToken.None);
            forced.OverrideState(StatusComponentState.Maintenance);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        probe.Fail(url);
        await RunPassAsync(host);

        var component = await ReadComponentAsync(host, id);
        component.CurrentState.Should().Be(StatusComponentState.Maintenance);
        component.ConsecutiveFailures.Should().Be(1);
        component.ActiveIncidentId.Should().NotBeNull();

        using var clearScope = host.Services.CreateScope();
        var clearContext = clearScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var tracked = await clearContext.StatusComponents.FirstAsync(c => c.Id == id, CancellationToken.None);
        tracked.OverrideState(null);
        await clearContext.SaveChangesAsync(CancellationToken.None);
        (await ReadComponentAsync(host, id)).CurrentState.Should().Be(StatusComponentState.Outage);
    }

    /// <summary>
    /// A component whose monitoring was switched off while the job's incident was open is still visited, so the incident is closed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_ClosesTheIncident_OfAComponentWhoseMonitoringWasSwitchedOff()
    {
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(DateTimeOffset.UtcNow));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 1, enabled: true);
        probe.Fail(url);
        await RunPassAsync(host);
        var incidentId = (await ReadComponentAsync(host, id)).ActiveIncidentId!.Value;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var component = await context.StatusComponents.FirstAsync(c => c.Id == id, CancellationToken.None);
            component.ConfigureMonitoring(url, false, 1);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await RunPassAsync(host);

        (await ReadComponentAsync(host, id)).ActiveIncidentId.Should().BeNull();
        (await ReadIncidentAsync(host, incidentId)).State.Should().Be(StatusIncidentState.Resolved);
    }

    /// <summary>The pass deletes the records older than the retention and keeps the newer ones.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Pass_DeletesTheRecordsOlderThanTheRetention()
    {
        var now = DateTimeOffset.UtcNow;
        var probe = new FakeStatusProbe();
        var host = HostWith(probe, new ManualTimeProvider(now));
        var url = $"https://{Guid.NewGuid():N}.example.test/health";
        var id = await SeedComponentAsync(host, url, 2, enabled: true);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var component = await context.StatusComponents.FirstAsync(c => c.Id == id, CancellationToken.None);
            context.StatusChecks.Add(component.RecordCheck(now.AddDays(-101), true, 10));
            context.StatusChecks.Add(component.RecordCheck(now.AddDays(-95), true, 10));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await RunPassAsync(host);

        using var readScope = host.Services.CreateScope();
        var remaining = await readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>()
            .StatusChecks.AsNoTracking().Where(c => c.ComponentId == id).Select(c => c.CheckedAt).ToListAsync(CancellationToken.None);
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(at => at < now.AddDays(-100));
    }
}
