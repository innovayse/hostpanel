using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/registry/{id}</c>: the registry's page and versions are set beside the installed state, the
/// stored licence key is sent and never returned, each version says whether it runs on the running host, an unknown
/// plugin answers <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c>, registry failures keep their codes, and the route is closed to
/// callers who are not administrators.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRegistryPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the details page.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}";

    /// <summary>The route of the licence key.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string LicenceRoute(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/licence";

    /// <summary>A plugin that is not installed answers every field of the page, with the versions newest first and no installed state.</summary>
    [Fact]
    public async Task ANotInstalledPlugin_AnswersTheWholePageAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1], category: PluginRegistryCategory.Payment);
        var summary = new RegistryPluginDto(
            pluginId, "Sample gateway", "Example Publisher", "Takes card payments.", "https://registry.example.com/icons/a.svg",
            PluginRegistryCategory.Payment, "1.2.0", "1.0", true, PluginRegistryPricing.Paid);
        registry.Details[pluginId] = new RegistryPluginDetails(
            summary,
            "MIT",
            "https://example.com/repo",
            "https://example.com/buy",
            "https://example.com/contact",
            "https://example.com/docs",
            "https://example.com/support",
            null);
        registry.Versions[pluginId] =
        [
            new RegistryPluginVersion(
                "1.0.0", "1.0", 10, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), PluginRegistryVersionStatus.Yanked,
                "A defect.", "1.1.0", null, null, "First release.", [], null, []),
            new RegistryPluginVersion(
                "1.2.0", "1.0", 20, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), PluginRegistryVersionStatus.Published,
                null, null, "0.5.0", "9.0.0", "Second release.\nMore.", [PluginRegistryCapability.Process, PluginRegistryCapability.FileSystem],
                null, [new RegistryPluginImage("https://registry.example.com/s/1.png", "image/png", 800, 600)]),
        ];
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, hostVersion: "1.4.0");

        var response = await client.GetAsync(Route(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(pluginId);
        body.GetProperty("name").GetString().Should().Be("Sample gateway");
        body.GetProperty("author").GetString().Should().Be("Example Publisher");
        body.GetProperty("description").GetString().Should().Be("Takes card payments.");
        body.GetProperty("iconUrl").GetString().Should().Be("https://registry.example.com/icons/a.svg");
        body.GetProperty("category").GetString().Should().Be("Payment");
        body.GetProperty("pricing").GetString().Should().Be("Paid");
        body.GetProperty("verified").GetBoolean().Should().BeTrue();
        body.GetProperty("latestVersion").GetString().Should().Be("1.2.0");
        body.GetProperty("installed").GetBoolean().Should().BeFalse();
        body.GetProperty("installedVersion").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("stagedVersion").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("updateAvailable").GetBoolean().Should().BeFalse();
        body.GetProperty("requiresLicence").GetBoolean().Should().BeTrue();
        body.GetProperty("hasLicenceKey").GetBoolean().Should().BeFalse();
        body.GetProperty("buyUrl").GetString().Should().Be("https://example.com/buy");
        body.GetProperty("documentationUrl").GetString().Should().Be("https://example.com/docs");
        body.GetProperty("supportUrl").GetString().Should().Be("https://example.com/support");
        body.GetProperty("repositoryUrl").GetString().Should().Be("https://example.com/repo");
        body.GetProperty("licence").GetString().Should().Be("MIT");

        var versions = body.GetProperty("versions").EnumerateArray().ToList();
        versions.Select(v => v.GetProperty("version").GetString()).Should().Equal("1.2.0", "1.0.0");
        var newest = versions[0];
        newest.GetProperty("publishedAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        newest.GetProperty("status").GetString().Should().Be("Published");
        newest.GetProperty("statusReason").ValueKind.Should().Be(JsonValueKind.Null);
        newest.GetProperty("replacementVersion").ValueKind.Should().Be(JsonValueKind.Null);
        newest.GetProperty("changelog").GetString().Should().Be("Second release.\nMore.");
        newest.GetProperty("minHostVersion").GetString().Should().Be("0.5.0");
        newest.GetProperty("maxHostVersion").GetString().Should().Be("9.0.0");
        newest.GetProperty("compatible").GetBoolean().Should().BeTrue();
        newest.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Equal("Process", "FileSystem");
        var shot = newest.GetProperty("screenshots").EnumerateArray().Single();
        shot.GetProperty("url").GetString().Should().Be("https://registry.example.com/s/1.png");
        shot.GetProperty("contentType").GetString().Should().Be("image/png");
        shot.GetProperty("width").GetInt32().Should().Be(800);
        shot.GetProperty("height").GetInt32().Should().Be(600);
        var old = versions[1];
        old.GetProperty("status").GetString().Should().Be("Yanked");
        old.GetProperty("statusReason").GetString().Should().Be("A defect.");
        old.GetProperty("replacementVersion").GetString().Should().Be("1.1.0");
        old.GetProperty("permissions").GetArrayLength().Should().Be(0);
        old.GetProperty("screenshots").GetArrayLength().Should().Be(0);
    }

    /// <summary>An installed plugin shows its version, a staged upgrade and whether a newer one is published.</summary>
    [Fact]
    public async Task AnInstalledPlugin_ShowsTheInstalledStateAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "2.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var before = await client.GetFromJsonAsync<JsonElement>(Route(pluginId));
            before.GetProperty("installed").GetBoolean().Should().BeTrue();
            before.GetProperty("installedVersion").GetString().Should().Be("1.0.0");
            before.GetProperty("stagedVersion").ValueKind.Should().Be(JsonValueKind.Null);
            before.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();

            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"));

            var after = await client.GetFromJsonAsync<JsonElement>(Route(pluginId));
            after.GetProperty("installedVersion").GetString().Should().Be("1.0.0");
            after.GetProperty("stagedVersion").GetString().Should().Be("2.0.0");
            after.GetProperty("updateAvailable").GetBoolean().Should().BeFalse("the newest version already waits for the restart");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A free plugin does not ask for a licence key.</summary>
    [Fact]
    public async Task AFreePlugin_DoesNotRequireALicenceAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var body = await client.GetFromJsonAsync<JsonElement>(Route(pluginId));

        body.GetProperty("requiresLicence").GetBoolean().Should().BeFalse();
    }

    /// <summary>The stored licence key is sent to the registry, reported as <c>hasLicenceKey</c> and never returned.</summary>
    [Fact]
    public async Task TheStoredLicenceKey_IsSentAndNeverReturnedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1], pricing: PluginRegistryPricing.Paid);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        const string key = "LICENCE-KEY-7f3a9c";

        try
        {
            (await client.PutAsJsonAsync(LicenceRoute(pluginId), new { key })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var response = await client.GetAsync(Route(pluginId));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var text = await response.Content.ReadAsStringAsync();
            text.Should().NotContain(key);
            using var json = JsonDocument.Parse(text);
            json.RootElement.GetProperty("hasLicenceKey").GetBoolean().Should().BeTrue();
            json.RootElement.GetProperty("requiresLicence").GetBoolean().Should().BeTrue();
            registry.LastLicenceKey.Should().Be(key);
        }
        finally
        {
            await client.DeleteAsync(LicenceRoute(pluginId));
        }
    }

    /// <summary>Without a stored key the registry is sent none.</summary>
    [Fact]
    public async Task WithoutAStoredKey_NoneIsSentAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        (await client.GetAsync(Route(pluginId))).StatusCode.Should().Be(HttpStatusCode.OK);

        registry.LastLicenceKey.Should().BeNull();
    }

    /// <summary>Each version is judged against the running host: below the minimum, above the maximum and inside the range.</summary>
    /// <param name="hostVersion">The version the host reports.</param>
    /// <param name="min">The version's lower bound.</param>
    /// <param name="max">The version's upper bound.</param>
    /// <param name="expected">Whether the version runs on that host.</param>
    [Theory]
    [InlineData("1.4.0", null, null, true)]
    [InlineData("1.4.0", "1.4.0", "1.4.0", true)]
    [InlineData("1.4.0", "1.5.0", null, false)]
    [InlineData("1.4.0", null, "1.3.9", false)]
    [InlineData("1.4.0", "1.0.0", "2.0.0", true)]
    [InlineData("dev", "1.0.0", "2.0.0", true)]
    [InlineData("1.4.0", "not-a-version", null, false)]
    public async Task Compatible_FollowsTheHostRangeAsync(string hostVersion, string? min, string? max, bool expected)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-det-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1], minHostVersion: min, maxHostVersion: max);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, hostVersion: hostVersion);

        var body = await client.GetFromJsonAsync<JsonElement>(Route(pluginId));

        body.GetProperty("versions")[0].GetProperty("compatible").GetBoolean().Should().Be(expected);
    }

    /// <summary>A plugin the registry does not list answers 404 with <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task AnUnknownPlugin_AnswersNotFoundAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync(Route($"test-none-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
    }

    /// <summary>Registry failures keep their codes and statuses.</summary>
    /// <param name="failureCode">The registry's failure.</param>
    /// <param name="status">The status the code maps to.</param>
    [Theory]
    [InlineData("PLUGIN_REGISTRY_DISABLED", HttpStatusCode.Conflict)]
    [InlineData("PLUGIN_REGISTRY_UNAVAILABLE", HttpStatusCode.BadGateway)]
    [InlineData("PLUGIN_REGISTRY_RESPONSE_INVALID", HttpStatusCode.BadGateway)]
    [InlineData("PLUGIN_REGISTRY_TIMED_OUT", HttpStatusCode.GatewayTimeout)]
    [InlineData("PLUGIN_REGISTRY_LICENCE_REQUIRED", HttpStatusCode.UnprocessableEntity)]
    [InlineData("PLUGIN_REGISTRY_LICENCE_INVALID", HttpStatusCode.UnprocessableEntity)]
    public async Task RegistryFailures_KeepTheirCodesAsync(string failureCode, HttpStatusCode status)
    {
        var registry = new FakePluginRegistryClient { FailureCode = failureCode };
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync(Route("some-plugin"));

        response.StatusCode.Should().Be(status);
        await PluginsTestSeeds.AssertCodeAsync(response, failureCode);
    }

    /// <summary>The real client with the default configuration answers <c>PLUGIN_REGISTRY_DISABLED</c>.</summary>
    [Fact]
    public async Task TheDefaultConfiguration_AnswersDisabledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync(Route("some-plugin"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
    }

    /// <summary>An id of the wrong shape is refused before the registry is asked.</summary>
    /// <param name="pluginId">The route id.</param>
    [Theory]
    [InlineData("Bad_Id")]
    [InlineData("UPPER")]
    [InlineData("a..b")]
    public async Task AMalformedId_IsRefusedAsync(string pluginId)
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync(Route(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        registry.LastLicenceKey.Should().BeNull();
    }

    /// <summary>The updates route is not taken for a plugin called "updates".</summary>
    [Fact]
    public async Task TheUpdatesRoute_StaysTheUpdateListAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync("/api/admin/plugins/registry/updates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "get-registry-plugin-forbidden");

        var response = await client.GetAsync(Route("some-plugin"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync(Route("some-plugin"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
