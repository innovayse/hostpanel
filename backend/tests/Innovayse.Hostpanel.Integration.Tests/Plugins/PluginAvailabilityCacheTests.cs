using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Time;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="PluginAvailabilityCache"/>, alone over a hand-moved clock and inside a host with a test app plugin: the
/// available set is computed once per lifetime, read again once it expires, dropped by an invalidation (and not stored
/// by a pass an invalidation overtook), and switching a plugin on through the admin API shows its page in the index at
/// once, while a switch written behind the use cases' back shows only after the lifetime.
/// </summary>
public sealed class PluginAvailabilityCacheTests : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The admin area index.</summary>
    private const string AdminIndexUrl = "/api/admin/plugins/ui";

    /// <summary>The admin page the test plugin declares.</summary>
    private const string AdminPage = "overview";

    /// <summary>The shared API and database the hosts are derived from.</summary>
    private readonly IntegrationTestFactory _factory;

    /// <summary>Initializes the test.</summary>
    /// <param name="factory">The shared API and database the hosts are derived from.</param>
    public PluginAvailabilityCacheTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Within the lifetime the set is computed once; past it, the next read computes it again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetOrCompute_WithinLifetime_ComputesOnce_AndAgainAfterExpiry()
    {
        var time = new ManualTimeProvider(TestMoments.Seeded);
        using var cache = new PluginAvailabilityCache(new SystemClock(time));
        var passes = 0;

        Task<IReadOnlySet<string>> Compute(CancellationToken ct)
        {
            passes++;
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal) { $"pass-{passes}" });
        }

        var first = await cache.GetOrComputeAsync(Compute, CancellationToken.None);
        time.Advance(PluginAvailabilityCache.Lifetime - TimeSpan.FromSeconds(1));
        var cached = await cache.GetOrComputeAsync(Compute, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        var expired = await cache.GetOrComputeAsync(Compute, CancellationToken.None);

        passes.Should().Be(2);
        first.Should().BeEquivalentTo(["pass-1"]);
        cached.Should().BeSameAs(first);
        expired.Should().BeEquivalentTo(["pass-2"]);
    }

    /// <summary>An invalidation drops a fresh set, so the next read computes it again although the lifetime has not passed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Invalidate_DropsAFreshSet()
    {
        using var cache = new PluginAvailabilityCache(new SystemClock(new ManualTimeProvider(TestMoments.Seeded)));
        var passes = 0;

        Task<IReadOnlySet<string>> Compute(CancellationToken ct)
        {
            passes++;
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        await cache.GetOrComputeAsync(Compute, CancellationToken.None);
        new PluginAvailabilityInvalidator(cache).Invalidate();
        await cache.GetOrComputeAsync(Compute, CancellationToken.None);

        passes.Should().Be(2);
    }

    /// <summary>
    /// A pass that an invalidation overtakes answers its own request but is not remembered, because the switches it read
    /// may already be stale; the next read computes again.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Invalidate_DuringAPass_KeepsThatPassFromBeingRemembered()
    {
        using var cache = new PluginAvailabilityCache(new SystemClock(new ManualTimeProvider(TestMoments.Seeded)));
        var passes = 0;

        Task<IReadOnlySet<string>> Compute(CancellationToken ct)
        {
            passes++;
            if (passes == 1)
            {
                cache.Invalidate();
            }

            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        await cache.GetOrComputeAsync(Compute, CancellationToken.None);
        await cache.GetOrComputeAsync(Compute, CancellationToken.None);
        await cache.GetOrComputeAsync(Compute, CancellationToken.None);

        passes.Should().Be(2);
    }

    /// <summary>
    /// Switching a plugin on through the admin integrations API invalidates the remembered set, so the very next index
    /// lists its page although the clock has not moved and the index answered without it a moment before.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EnablingThroughTheAdminApi_ShowsThePageAtOnce()
    {
        var pluginId = PluginActivationTestSeeds.NewId("avail-api");
        using var host = HostWith(pluginId, new ManualTimeProvider(TestMoments.Seeded));
        using var admin = await AdminClientAsync(host);

        (await AdminPageKeysAsync(admin)).Should().NotContain($"{pluginId}/{AdminPage}");

        var save = await admin.PutAsJsonAsync(
            $"/api/admin/integrations/{pluginId}",
            new { isEnabled = true, config = new Dictionary<string, string>() });
        save.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AdminPageKeysAsync(admin)).Should().Contain($"{pluginId}/{AdminPage}");
    }

    /// <summary>
    /// A switch written straight into the table, past every use case, is not seen while the remembered set is fresh and
    /// is seen once the lifetime has passed: the lifetime bounds how long any change can go unseen.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SwitchWrittenBehindTheUseCases_ShowsOnlyAfterTheLifetime()
    {
        var pluginId = PluginActivationTestSeeds.NewId("avail-ttl");
        var time = new ManualTimeProvider(TestMoments.Seeded);
        using var host = HostWith(pluginId, time);
        using var admin = await AdminClientAsync(host);

        (await AdminPageKeysAsync(admin)).Should().NotContain($"{pluginId}/{AdminPage}");

        using (var scope = host.Services.CreateScope())
        {
            await PluginActivationTestSeeds.EnableAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), pluginId);
        }

        (await AdminPageKeysAsync(admin)).Should().NotContain($"{pluginId}/{AdminPage}");

        time.Advance(PluginAvailabilityCache.Lifetime);

        (await AdminPageKeysAsync(admin)).Should().Contain($"{pluginId}/{AdminPage}");
    }

    /// <summary>Builds a host whose registry holds one app plugin, switched off, with one admin page, and whose clock is <paramref name="time"/>.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="time">The hand-moved time source the host reads.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private WebApplicationFactory<Program> HostWith(string pluginId, ManualTimeProvider time)
    {
        var registry = PluginActivationTestSeeds.Registry(AppPluginWithAdminPage(pluginId));
        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PluginRegistry>();
                services.RemoveAll<IPluginRegistry>();
                services.AddSingleton(registry);
                services.AddSingleton<IPluginRegistry>(registry);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(time);
            }));
    }

    /// <summary>Makes an administrator's client of a host.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <returns>The client, to dispose when the test ends.</returns>
    private async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> host)
    {
        return await PluginUiFixture.AdminClientOfAsync(host, _factory);
    }

    /// <summary>Reads the admin index and returns its pages as <c>pluginId/id</c>.</summary>
    /// <param name="admin">The administrator's client.</param>
    /// <returns>The page keys, in the order answered.</returns>
    private static async Task<List<string>> AdminPageKeysAsync(HttpClient admin)
    {
        var response = await admin.GetAsync(AdminIndexUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("pages").EnumerateArray()
            .Select(row => $"{row.GetProperty("pluginId").GetString()}/{row.GetProperty("id").GetString()}")
            .ToList();
    }

    /// <summary>An app plugin that declares one admin page with a small view and requires no setting.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin AppPluginWithAdminPage(string pluginId)
    {
        var baseline = PluginActivationTestSeeds.Plugin(pluginId, PluginType.App, typeof(FakeAppPlugin)).Manifest;
        var manifest = new PluginManifest
        {
            ManifestVersion = 2,
            Id = baseline.Id,
            Name = baseline.Name,
            Version = baseline.Version,
            Author = baseline.Author,
            Description = baseline.Description,
            Type = PluginType.App,
            Category = baseline.Category,
            EntryPoint = baseline.EntryPoint,
            SdkVersion = baseline.SdkVersion,
            Ui = new PluginUi
            {
                Admin = new PluginUiArea
                {
                    Pages = [new PluginPage { Id = AdminPage, Path = $"/{AdminPage}", Title = "Overview", View = $"views/{AdminPage}.json" }],
                },
            },
        };
        var views = new Dictionary<string, JsonElement>
        {
            [$"views/{AdminPage}.json"] = PluginUiFixture.View("""{ "schema": 1, "blocks": [ { "kind": "header", "title": "Overview" } ] }"""),
        };

        return new LoadedPlugin(manifest, typeof(FakeAppPlugin), new PluginViewDocuments(views));
    }
}
