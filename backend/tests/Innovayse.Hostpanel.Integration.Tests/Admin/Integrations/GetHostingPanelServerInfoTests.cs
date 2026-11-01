using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>GET /api/admin/integrations/cwp/server-info</c>: a successful probe answers 200 connected
/// with the provider's reported account count and version, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential. This use case always
/// succeeds; a failed probe is reported, not refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetHostingPanelServerInfoTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A successful hosting panel probe answers 200 connected with the provider's reported account count and
    /// version. Seeds the setting rows directly: <c>GetHostingPanelServerInfoHandler</c> reads a fixed
    /// <c>integration:innovayse-cwp:*</c> prefix, which the config-save route (writing under the requested slug, here "cwp") does
    /// not reach.
    /// </summary>
    [Fact]
    public async Task WhenProbeSucceeds_ReportsConnectedAsync()
    {
        var cwp = new FakeHostingPanelApiClient { AccountsCount = 42, Version = "1.9.1" };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelApiClient>(cwp)));

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await ReplaceSettingAsync(context, IntegrationSettingKeys.HostField, "panel.example.com");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.PortField, "2304");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.ApiKeyField, "test-api-key");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/integrations/cwp/server-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("connected").GetBoolean().Should().BeTrue();
        body.GetProperty("accountsCount").GetInt32().Should().Be(42);
        body.GetProperty("version").GetString().Should().Be("1.9.1");
        cwp.Connections.Should().ContainSingle()
            .Which.Should().Be(new HostingPanelConnection("panel.example.com", 2304, "test-api-key"));
    }

    /// <summary>
    /// A stored port that is not a number falls back to <see cref="IHostingPanelApiClient.DefaultApiPort"/>
    /// rather than reaching the panel on a port nobody configured.
    /// </summary>
    [Fact]
    public async Task WhenPortIsNotANumber_ProbesTheDefaultApiPortAsync()
    {
        var cwp = new FakeHostingPanelApiClient();
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelApiClient>(cwp)));

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await ReplaceSettingAsync(context, IntegrationSettingKeys.HostField, "panel-default-port.example.com");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.PortField, "not-a-port");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.ApiKeyField, "test-api-key");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/integrations/cwp/server-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cwp.Connections.Should().ContainSingle()
            .Which.Port.Should().Be(IHostingPanelApiClient.DefaultApiPort);
    }

    /// <summary>
    /// A failed probe still answers 200 with <c>connected = false</c> and puts neither the panel's
    /// host nor the provider's own exception text on the wire.
    /// </summary>
    [Fact]
    public async Task WhenProbeFails_NeverLeaksProviderDetailAsync()
    {
        var cwp = new FakeHostingPanelApiClient { ShouldThrow = true };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelApiClient>(cwp)));

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await ReplaceSettingAsync(context, IntegrationSettingKeys.HostField, "panel-unreachable.example.com");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.PortField, "2304");
            await ReplaceSettingAsync(context, IntegrationSettingKeys.ApiKeyField, "test-api-key");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/integrations/cwp/server-info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("panel-unreachable.example.com")
            .And.NotContain("nobody should see")
            .And.NotContain("test-api-key");
        using var doc = JsonDocument.Parse(text);
        doc.RootElement.GetProperty("connected").GetBoolean().Should().BeFalse();
        cwp.Connections.Should().ContainSingle();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "panel-server-info-forbidden");

        var response = await client.GetAsync("/api/admin/integrations/cwp/server-info");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/integrations/cwp/server-info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Stores one hosting panel setting, overwriting a value an earlier test in this class left
    /// behind (the key is unique and the database is shared by the class).
    /// </summary>
    /// <param name="context">The data context to write through.</param>
    /// <param name="field">The setting field under the hosting panel prefix.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>A task that completes once the change is tracked.</returns>
    private static async Task ReplaceSettingAsync(IHostpanelDbContext context, string field, string value)
    {
        var key = IntegrationSettingKeys.FieldKey(IntegrationSlugs.InnovayseCwp, field);
        var existing = await context.Settings.FirstOrDefaultAsync(s => s.Key == key, CancellationToken.None);
        if (existing is null)
        {
            context.Settings.Add(Setting.Create(key, value, null));
        }
        else
        {
            existing.UpdateValue(value);
        }
    }
}
