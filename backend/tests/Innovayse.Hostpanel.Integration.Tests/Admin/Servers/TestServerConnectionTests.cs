using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>POST /api/admin/servers/{id}/test-connection</c>: a successful probe reports connected with
/// the provider's account count and persists the server as online, a failed probe still answers
/// 200 without leaking provider detail, an unknown id is refused <c>SERVER_NOT_FOUND</c>, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TestServerConnectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A successful probe answers 200 with the provider's reported account count, and persists the
    /// server as online.
    /// </summary>
    [Fact]
    public async Task WhenProviderSucceeds_ReportsConnectedAsync()
    {
        var cwp7 = new FakeHostingPanelV7ApiClient { PackagesCount = 11 };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelV7ApiClient>(cwp7)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var response = await client.PostAsync($"/api/admin/servers/{id}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("connected").GetBoolean().Should().BeTrue();
        body.GetProperty("accountsCount").GetInt32().Should().Be(11);
    }

    /// <summary>
    /// A failed probe still answers 200 (the diagnostic result is the answer the caller asked
    /// for) and never puts the provider's own exception text on the wire.
    /// </summary>
    [Fact]
    public async Task WhenProviderFails_ReportsDisconnectedWithoutLeakingDetailAsync()
    {
        var cwp7 = new FakeHostingPanelV7ApiClient { ShouldThrow = true };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelV7ApiClient>(cwp7)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var response = await client.PostAsync($"/api/admin/servers/{id}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("connection details nobody should see");

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        doc.RootElement.GetProperty("connected").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("errorMessage").GetString()
            .Should().Be("Could not reach the server. See the server log for detail.");
    }

    /// <summary>A failed probe words its reason in the language the request asked for, not in English only.</summary>
    [Fact]
    public async Task WhenProviderFails_WordsTheReasonInTheRequestLanguageAsync()
    {
        var cwp7 = new FakeHostingPanelV7ApiClient { ShouldThrow = true };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostingPanelV7ApiClient>(cwp7)));

        var token = await factory.GetAdminTokenAsync();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var id = await ServersTestSeeds.CreateServerAsync(client, $"Server-{Guid.NewGuid():N}");

        var response = await client.PostAsync($"/api/admin/servers/{id}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("errorMessage").GetString()
            .Should().Be("Не удалось связаться с сервером. Подробности в журнале сервера.");
    }

    /// <summary>
    /// A server whose host is the cloud metadata address is never dialled: the outbound guard
    /// refuses it, and the test reports the server as unreachable.
    /// </summary>
    [Fact]
    public async Task MetadataAddress_IsRefusedAsUnreachableAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        var update = await client.PutAsJsonAsync(
            $"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, hostname: "169.254.169.254"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PostAsync($"/api/admin/servers/{id}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("connected").GetBoolean().Should().BeFalse();
    }

    /// <summary>Testing the connection of an unknown server id answers 404 with <c>SERVER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/admin/servers/{ServersTestSeeds.NoSuchId}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "test-server-connection-forbidden");

        var response = await client.PostAsync($"/api/admin/servers/{ServersTestSeeds.NoSuchId}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}/test-connection", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
