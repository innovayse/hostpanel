using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>GET /api/admin/integrations/{slug}</c>: an admin reads one integration's full configuration
/// with secret fields answered as set or not set and never by value, an unrecognised slug is refused <c>INTEGRATION_NOT_FOUND</c>, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetIntegrationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Saving the mail server's password, then reading it back, shows only that it is set.</summary>
    [Fact]
    public async Task SaveThenGet_WithholdsTheSecretAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var save = await client.PutAsJsonAsync("/api/admin/integrations/smtp", new
        {
            isEnabled = true,
            config = new Dictionary<string, string> { ["password"] = "secret_should_never_be_echoed_back" },
        });
        save.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetFromJsonAsync<JsonElement>("/api/admin/integrations/smtp");
        get.GetProperty("name").GetString().Should().Be("SMTP Server");
        get.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        get.GetRawText().Should().NotContain("secret_should_never_be_echoed_back");
        var secret = get.GetProperty("config").GetProperty("password");
        secret.GetProperty("isSecret").GetBoolean().Should().BeTrue();
        secret.GetProperty("hasValue").GetBoolean().Should().BeTrue();
        secret.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A loaded plugin with an icon reads <c>isPlugin: true</c> and the icon endpoint; a built-in reads false and null.</summary>
    [Fact]
    public async Task Detail_NamesThePluginAndItsIconUrlAsync()
    {
        var pluginId = $"icon-detail-{Guid.NewGuid():N}";
        var registry = new FakePluginRegistry();
        registry.Add(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson(pluginId))!);
        registry.AddIcon(pluginId, new PluginIconDto("image/png", [1, 2, 3], "\"t\""));
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPluginRegistry>(registry)));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var plugin = await client.GetFromJsonAsync<JsonElement>($"/api/admin/integrations/{pluginId}");
        var builtIn = await client.GetFromJsonAsync<JsonElement>("/api/admin/integrations/smtp");

        plugin.GetProperty("isPlugin").GetBoolean().Should().BeTrue();
        plugin.GetProperty("iconUrl").GetString().Should().Be($"/api/admin/plugins/{pluginId}/icon");
        builtIn.GetProperty("isPlugin").GetBoolean().Should().BeFalse();
        builtIn.GetProperty("iconUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An unrecognised slug answers 404 with <c>INTEGRATION_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownSlug_RefusesNotFoundAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/integrations/unknown-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await IntegrationsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "get-integration-forbidden");

        var response = await client.GetAsync("/api/admin/integrations/smtp");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/integrations/smtp");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
