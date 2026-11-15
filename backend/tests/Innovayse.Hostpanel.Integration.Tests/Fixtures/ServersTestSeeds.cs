using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared seeding and authentication helpers for the Admin/Servers module's per-use-case
/// Integration tests: creating a server or a server group through the admin routes, an
/// authenticated admin or Client-role caller, and reading a refusal's error code. Every
/// <c>Admin/Servers/*Tests.cs</c> file calls into this rather than repeating its own copy.
/// </summary>
internal static class ServersTestSeeds
{
    /// <summary>Shared test password for every client user this class registers.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>An id no seeded or created server or group ever takes.</summary>
    public const int NoSuchId = 999_999_999;

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client with the Admin role.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Registers a brand-new client user and returns an authenticated <see cref="HttpClient"/>
    /// carrying only the Client role: the caller every admin-only boundary test refuses.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">A short label folded into the unique email, for readable test failures.</param>
    /// <returns>An authenticated client with no elevated role.</returns>
    public static async Task<HttpClient> ClientRoleClientAsync(IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Server",
            lastName = "Boundary",
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Asserts the refusal carries the expected error code.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>Builds the admin create or update body for a server, grouped the way the API binds it.</summary>
    /// <param name="name">The server's display name.</param>
    /// <param name="hostname">The API hostname; defaults to one derived from <paramref name="name"/>.</param>
    /// <param name="module">The control panel module.</param>
    /// <param name="accessHash">The access hash to send, or null to send none.</param>
    /// <param name="isDefault">Whether the server is the default for its module.</param>
    /// <param name="monthlyCost">The monthly cost.</param>
    /// <param name="clearAccessHash">Whether to ask for the stored access hash to be removed.</param>
    /// <param name="ipAddress">The server's IP address, or null to record none.</param>
    /// <returns>The body.</returns>
    public static object ServerBody(
        string name,
        string? hostname = null,
        ServerModule module = ServerModule.Cwp7,
        string? accessHash = null,
        bool isDefault = false,
        decimal monthlyCost = 0m,
        bool clearAccessHash = false,
        string? ipAddress = null) => new
    {
        name,
        endpoint = new ServerEndpointDto(hostname ?? $"{name}.example.com", ipAddress, null, true),
        module,
        username = "root",
        credentials = new ServerCredentials(null, null, accessHash),
        limits = new ServerLimitsDto(null, isDefault, false),
        hosting = new ServerHostingDto(null, null, monthlyCost),
        nameServers = ServerNameServers.None,
        clears = new ServerCredentialClears(false, false, clearAccessHash),
    };

    /// <summary>Creates a server with a fresh name and returns its assigned id.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="name">The server's display name.</param>
    /// <param name="module">The control panel module.</param>
    /// <param name="hostname">The API hostname; defaults to one derived from <paramref name="name"/>.</param>
    /// <param name="ipAddress">The server's IP address, or null to record none.</param>
    /// <returns>The new server's id.</returns>
    public static async Task<int> CreateServerAsync(
        HttpClient client,
        string name,
        ServerModule module = ServerModule.Cwp7,
        string? hostname = null,
        string? ipAddress = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/admin/servers",
            ServerBody(name, hostname, module, accessHash: "top-secret-access-hash", monthlyCost: 12.5m, ipAddress: ipAddress));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Reads, from the paged admin list, the servers whose name, host name or address contains the text.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="search">The text to look for; a server's unique name finds that server alone.</param>
    /// <returns>The rows of the first page, which holds every match a test makes.</returns>
    public static async Task<List<JsonElement>> SearchServersAsync(HttpClient client, string search)
    {
        var page = await PagedListReads.PageAsync(client, $"/api/admin/servers?page=1&pageSize=100&search={Uri.EscapeDataString(search)}");
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>Records the outcome of a connection test on a server, as a test of the connection would, without calling a provider.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="serverId">The server the test is recorded on.</param>
    /// <param name="isOnline">Whether the recorded connection succeeded.</param>
    /// <param name="accountsCount">The account count the test reported, or null.</param>
    /// <returns>A task that completes once the outcome is saved.</returns>
    public static async Task RecordConnectionTestAsync(IntegrationTestFactory factory, int serverId, bool isOnline, int? accountsCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = await context.Servers.SingleAsync(s => s.Id == serverId);
        server.RecordConnectionTest(isOnline, accountsCount, TestMoments.Seeded);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Creates a group with the given name and server membership.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="name">The group's display name.</param>
    /// <param name="serverIds">The servers to assign to the new group.</param>
    /// <returns>The new group's id.</returns>
    public static async Task<int> CreateServerGroupAsync(HttpClient client, string name, params int[] serverIds)
    {
        var response = await client.PostAsJsonAsync("/api/admin/server-groups", new
        {
            name,
            fillType = ServerFillType.LeastFull,
            serverIds,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<int>();
    }
}
