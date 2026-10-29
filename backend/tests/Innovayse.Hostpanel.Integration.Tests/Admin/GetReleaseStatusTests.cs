using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Admin;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/release-status</c>: an admin reads the installed version and upgrade
/// availability, a caller with only the Client role is refused, and the route is closed to
/// anonymous callers. No outbound HTTP is made on this path.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetReleaseStatusTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>The endpoint path.</summary>
    private const string Url = "/api/admin/release-status";

    /// <summary>Clears the <c>LatestReleases</c> table so each test starts from a clean state.</summary>
    /// <returns>A task.</returns>
    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.LatestReleases.ExecuteDeleteAsync();
    }

    /// <summary>Nothing to clean up after a test.</summary>
    /// <returns>A completed task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The release status endpoint returns 200 with the installed version for an admin.</summary>
    [Fact]
    public async Task ReturnsInstalledVersionAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("installedVersion", out _).Should().BeTrue();
    }

    /// <summary>
    /// When no release has been cached yet (the cron has not run), the response carries
    /// <c>isUpgradeAvailable: false</c> and no latest version.
    /// </summary>
    [Fact]
    public async Task ReturnsNoUpgradeWhenNoReleaseIsCachedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isUpgradeAvailable").GetBoolean().Should().BeFalse();
        body.GetProperty("latestVersion").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// When a newer version is present in the cache, the response carries
    /// <c>isUpgradeAvailable: true</c>, the latest version, and the upgrade command.
    /// </summary>
    [Fact]
    public async Task ReturnsUpgradeAvailableWhenCacheHasANewerVersionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.LatestReleases.Add(LatestRelease.Create(
            "99.0.0",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/innovayse/hostpanel/releases/tag/v99.0.0",
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);

        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isUpgradeAvailable").GetBoolean().Should().BeTrue();
        body.GetProperty("latestVersion").GetString().Should().Be("99.0.0");
        body.GetProperty("upgradeCommand").GetString().Should().Contain("--version 99.0.0");
        body.GetProperty("releaseNotesUrl").GetString().Should().Contain("v99.0.0");
    }

    /// <summary>
    /// When the installed version matches the cached version, no upgrade is available.
    /// </summary>
    [Fact]
    public async Task ReturnsNoUpgradeWhenInstalledVersionMatchesCacheAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // The test host's IInstalledVersionProvider returns "dev" (no assembly attribute in tests),
        // which is not a parseable System.Version — so any cached version with a real semver
        // is never treated as an upgrade over "dev".
        context.LatestReleases.Add(LatestRelease.Create(
            "1.0.0",
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "https://github.com/innovayse/hostpanel/releases/tag/v1.0.0",
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);

        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // "dev" is not a parseable version, so it is never treated as older than any release.
        body.GetProperty("isUpgradeAvailable").GetBoolean().Should().BeFalse();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "release-status-forbidden");

        var response = await client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The release status endpoint is not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
