using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// A plugin integration on the admin Integrations page (#854, #855, #856): its setting texts come in the admin's
/// language from the plugin's locales (English where a locale lacks a key), Test Connection asks its
/// <c>IPluginStatus</c> within the host's limit and fails closed without leaking the plugin's exception text, a plugin
/// without <c>IPluginStatus</c> cannot be tested, and the list shows its connection state, cached until a test or a save.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginIntegrationStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The setting whose texts are keys of the manifest locales.</summary>
    private const string HostField = "host";

    /// <summary>Builds a loaded plugin with localised settings, implemented by <paramref name="implementation"/>.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="implementation">The plugin's CLR type.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, Type implementation)
    {
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Status plugin",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Plugin used by the plugin integration status tests.",
            Type = PluginType.Hosted,
            Category = "Hosted",
            EntryPoint = implementation.FullName!,
            SdkVersion = "1.0",
            ShowInIntegrations = true,
            Settings =
            [
                new PluginSetting
                {
                    Key = HostField,
                    Label = "settings.host",
                    Description = "settings.hostHelp",
                    Placeholder = "settings.hostPlaceholder",
                    Type = PluginSettingType.Url,
                },
                new PluginSetting
                {
                    Key = "region",
                    Label = "settings.region",
                    Type = PluginSettingType.Select,
                    Options = [new PluginSettingOption { Value = "eu", Label = "regions.eu" }],
                },
                new PluginSetting { Key = ScriptedStatusPlugin.BehaviourField, Label = "Behaviour", Type = PluginSettingType.Text },
            ],
            Locales = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = new Dictionary<string, string>
                {
                    ["settings.host"] = "Service address",
                    ["settings.hostHelp"] = "Base address of the API",
                    ["settings.hostPlaceholder"] = "https://api.example.org",
                    ["settings.region"] = "Region",
                    ["regions.eu"] = "Europe",
                    [ScriptedStatusPlugin.HealthyDetailKey] = "Both services answer.",
                },
                ["ru"] = new Dictionary<string, string>
                {
                    ["settings.host"] = "Адрес сервиса",
                    ["regions.eu"] = "Европа",
                    [ScriptedStatusPlugin.HealthyDetailKey] = "Оба сервиса отвечают.",
                },
            },
        };
        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }

    /// <summary>A host whose plugin registries hold only <paramref name="plugin"/>, with a one second status limit.</summary>
    /// <param name="plugin">The loaded plugin.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> Host(LoadedPlugin plugin)
    {
        var registry = PluginActivationTestSeeds.Registry(plugin);
        var integrations = new PluginIntegrationRegistry();
        integrations.Register(new PluginIntegrationEntry(
            plugin.Manifest.Id, plugin.Manifest.Name, "Hosted", plugin.Manifest.Type, "#123456", []));
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PluginApi:StatusTimeoutSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(registry);
                services.AddSingleton<IPluginRegistry>(registry);
                services.AddSingleton<IPluginIntegrationRegistry>(integrations);
            });
        });
    }

    /// <summary>An admin client of <paramref name="host"/> that asks in <paramref name="language"/>.</summary>
    /// <param name="host">The host.</param>
    /// <param name="language">The <c>X-Language</c> value.</param>
    /// <returns>The client.</returns>
    private async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> host, string language = "en")
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        client.DefaultRequestHeaders.Add("X-Language", language);
        return client;
    }

    /// <summary>Switches the plugin on and chooses how its status check answers.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="behaviour">The scripted answer.</param>
    /// <returns>A task that completes when both rows are stored.</returns>
    private async Task EnableAsync(string id, string behaviour)
    {
        await SecretSettingTestSeeds.StoreAsync(factory, IntegrationSettingKeys.EnabledKey(id), "true");
        await SecretSettingTestSeeds.StoreAsync(factory, IntegrationSettingKeys.FieldKey(id, ScriptedStatusPlugin.BehaviourField), behaviour);
    }

    /// <summary>Finds a field definition in a detail body.</summary>
    /// <param name="detail">The detail body.</param>
    /// <param name="key">The field key.</param>
    /// <returns>The field definition.</returns>
    private static JsonElement Field(JsonElement detail, string key)
    {
        return detail.GetProperty("fieldDefinitions").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key);
    }

    /// <summary>The detail answers every setting text in the admin's language, English where the locale lacks the key.</summary>
    [Fact]
    public async Task GetIntegration_ResolvesSettingTextsInTheRequestLanguageAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-texts");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))), "ru");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/integrations/{id}");

        var host = Field(detail, HostField);
        host.GetProperty("label").GetString().Should().Be("Адрес сервиса");
        host.GetProperty("description").GetString().Should().Be("Base address of the API");
        host.GetProperty("placeholder").GetString().Should().Be("https://api.example.org");
        var region = Field(detail, "region");
        region.GetProperty("label").GetString().Should().Be("Region");
        region.GetProperty("optionLabels").GetProperty("eu").GetString().Should().Be("Европа");
        region.GetProperty("options")[0].GetString().Should().Be("eu");
        detail.GetProperty("canTestConnection").GetBoolean().Should().BeTrue();
    }

    /// <summary>Test Connection asks the plugin and answers its own detail in the admin's language.</summary>
    [Fact]
    public async Task TestConnection_HealthyPlugin_ReportsSuccessInTheRequestLanguageAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-healthy");
        await EnableAsync(id, "healthy");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))), "ru");

        var response = await client.PostAsync($"/api/admin/integrations/{id}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Оба сервиса отвечают.");
        ScriptedStatusPlugin.CallsOf(id).Should().Be(1);
    }

    /// <summary>A throwing check fails closed and never puts the plugin's exception text on the wire.</summary>
    [Fact]
    public async Task TestConnection_ThrowingPlugin_FailsClosedWithoutLeakingAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-throw");
        await EnableAsync(id, "throw");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))));

        var response = await client.PostAsync($"/api/admin/integrations/{id}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(ScriptedStatusPlugin.SecretFailureText);
        using var doc = JsonDocument.Parse(raw);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    /// <summary>A plugin that ignores its token is abandoned at the host's limit and reported as not working.</summary>
    [Fact]
    public async Task TestConnection_HangingPlugin_TimesOutWithinTheLimitAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-hang");
        await EnableAsync(id, "hang");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))));
        var started = DateTimeOffset.UtcNow;

        var response = await client.PostAsync($"/api/admin/integrations/{id}/test", null);

        (DateTimeOffset.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(15));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Be("PLUGIN_STATUS_TIMED_OUT");
    }

    /// <summary>A switched-off plugin is not called and the test reports it as not active.</summary>
    [Fact]
    public async Task TestConnection_SwitchedOffPlugin_IsNotCalledAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-off");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))));

        var response = await client.PostAsync($"/api/admin/integrations/{id}/test", null);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        ScriptedStatusPlugin.CallsOf(id).Should().Be(0);
    }

    /// <summary>A plugin without <c>IPluginStatus</c> cannot be tested: the detail says so and the test is refused.</summary>
    [Fact]
    public async Task PluginWithoutStatus_CannotBeTestedAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-none");
        var client = await ClientAsync(Host(Plugin(id, typeof(object))));

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/integrations/{id}");
        var response = await client.PostAsync($"/api/admin/integrations/{id}/test", null);

        detail.GetProperty("canTestConnection").GetBoolean().Should().BeFalse();
        detail.GetProperty("connection").ValueKind.Should().Be(JsonValueKind.Null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await IntegrationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_NOT_TESTABLE);
    }

    /// <summary>
    /// The list shows a switched-on plugin's connection state, asks the plugin once for two loads (cached), and asks
    /// again after a save of the integration.
    /// </summary>
    [Fact]
    public async Task ListIntegrations_ShowsTheCachedConnectionStateAsync()
    {
        var id = PluginActivationTestSeeds.NewId("status-list");
        await EnableAsync(id, "degraded");
        var client = await ClientAsync(Host(Plugin(id, typeof(ScriptedStatusPlugin))));

        var first = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");
        await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");

        var row = first!.Single(i => i.GetProperty("slug").GetString() == id);
        var connection = row.GetProperty("connection");
        connection.GetProperty("state").GetString().Should().Be(nameof(IntegrationConnectionState.Degraded));
        connection.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        ScriptedStatusPlugin.CallsOf(id).Should().Be(1);

        var save = await client.PutAsJsonAsync($"/api/admin/integrations/{id}", new
        {
            isEnabled = true,
            config = new Dictionary<string, string> { [ScriptedStatusPlugin.BehaviourField] = "down" },
        });
        save.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");

        ScriptedStatusPlugin.CallsOf(id).Should().Be(2);
        after!.Single(i => i.GetProperty("slug").GetString() == id)
            .GetProperty("connection").GetProperty("state").GetString().Should().Be(nameof(IntegrationConnectionState.Unavailable));
    }
}
