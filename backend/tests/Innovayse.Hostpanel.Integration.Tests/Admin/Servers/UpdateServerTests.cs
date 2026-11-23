using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>PUT /api/admin/servers/{id}</c>: creating then updating a server round-trips its new name,
/// an unknown id is refused <c>SERVER_NOT_FOUND</c>, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating then updating a server round-trips its new name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenUpdate_RoundTripsAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        var newName = $"{name}-renamed";

        var update = await client.PutAsJsonAsync(
            $"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(newName, hostname: $"{name}.example.com", monthlyCost: 20m));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ServersTestSeeds.SearchServersAsync(client, newName)).Should().Contain(r => r.GetProperty("name").GetString() == newName);
    }

    /// <summary>The update route holds the API address to the same rule as the create route and keeps a valid one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApiHost_IsHeldToTheCreateRule_AndAValidOneIsStoredAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var refused = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, apiHost: "169.254.169.254"));
        var accepted = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, apiHost: "10.77.0.1"));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(refused, SharedErrorCodes.ValidationError);
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServersTestSeeds.SearchServersAsync(client, name)).Single().GetProperty("endpoint").GetProperty("apiHost").GetString().Should().Be("10.77.0.1");
    }

    /// <summary>
    /// A server stored with a module no installed plugin serves any more (its panel plugin was removed) can still be edited while the
    /// module stays the same, and is refused <c>SERVER_MODULE_UNKNOWN</c> once the module is changed to one nobody knows.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StoredModuleNoPluginServes_StaysEditableUnchangedAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Servers
                .Where(s => s.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Module, "RetiredPanel"));
        }

        var unchanged = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody($"{name}-renamed", module: "retiredpanel"));
        var changed = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, module: "no-such-panel"));

        unchanged.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ServersTestSeeds.SearchServersAsync(client, $"{name}-renamed")).Single().GetProperty("module").GetString().Should().Be("RetiredPanel");
        changed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(changed, HostpanelErrorCodes.SERVER_MODULE_UNKNOWN);
    }

    /// <summary>An update switches the self signed certificate choice on and off, and the list reads it back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelfSignedCertificate_IsStoredAndClearedByAnUpdateAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        (await ReadServerAsync(client, id, name)).GetProperty("endpoint").GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();

        (await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, acceptsSelfSignedCertificate: true)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id, name)).GetProperty("endpoint").GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeTrue();

        (await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, acceptsSelfSignedCertificate: false)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id, name)).GetProperty("endpoint").GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The list says a credential is stored without returning it; an update with the field empty
    /// keeps it, and an explicit clear removes it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Credentials_AreReportedKeptOnBlankAndRemovedOnClearAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var created = await ReadServerAsync(client, id, name);
        created.GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeTrue();
        created.GetProperty("credentials").GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        created.GetRawText().Should().NotContain("top-secret-access-hash");

        var blank = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, accessHash: string.Empty, monthlyCost: 12.5m, clearAccessHash: false));
        blank.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id, name)).GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeTrue();

        var cleared = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, accessHash: null, monthlyCost: 12.5m, clearAccessHash: true));
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id, name)).GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeFalse();
    }

    /// <summary>Updating an unknown server id answers 404 with <c>SERVER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}",
            ServersTestSeeds.ServerBody("Ghost", hostname: "ghost.example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "update-server-forbidden");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads one server from the admin list.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="id">The server's id.</param>
    /// <param name="name">The server's unique name, which finds it on the first page.</param>
    /// <returns>The server's row.</returns>
    private static async Task<System.Text.Json.JsonElement> ReadServerAsync(HttpClient client, int id, string name)
    {
        var list = await ServersTestSeeds.SearchServersAsync(client, name);
        return list.Single(r => r.GetProperty("id").GetInt32() == id);
    }
}
