using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Admin.Servers.Commands.CreateServer;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>POST /api/admin/servers</c>: a created server appears in the list without its stored
/// secret, an empty name is refused <c>VALIDATION_ERROR</c> by the validator before the handler,
/// a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential. The control panel module is an open id: a built in one is stored in the host's spelling whatever case was sent, one
/// an installed plugin declares is stored in the plugin's spelling, and one nobody knows is refused <c>SERVER_MODULE_UNKNOWN</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateServerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A created server appears in the list, and its stored secret never appears in the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenList_ShowsTheServerWithoutItsSecretAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";

        await ServersTestSeeds.CreateServerAsync(client, name);

        var row = (await ServersTestSeeds.SearchServersAsync(client, name)).Single(r => r.GetProperty("name").GetString() == name);
        row.GetRawText().Should().NotContain("top-secret-access-hash");
        row.GetProperty("credentials").TryGetProperty("accessHash", out _).Should().BeFalse("the DTO never carries a server's credentials");
    }

    /// <summary>A server is created trusting no self signed certificate unless the body says so, and the choice is stored and listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelfSignedCertificate_IsOffByDefaultAndStoredWhenAskedAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var plain = $"Server-{Guid.NewGuid():N}";
        var trusting = $"Server-{Guid.NewGuid():N}";

        (await client.PostAsJsonAsync("/api/admin/servers", ServersTestSeeds.ServerBody(plain))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync(
            "/api/admin/servers", ServersTestSeeds.ServerBody(trusting, acceptsSelfSignedCertificate: true))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ServersTestSeeds.SearchServersAsync(client, plain)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();
        (await ServersTestSeeds.SearchServersAsync(client, trusting)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeTrue();
    }

    /// <summary>A body that does not mention the choice (an older script) creates a server that trusts nothing extra.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelfSignedCertificate_OmittedFromTheBody_IsOffAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var body = new
        {
            name,
            endpoint = new { hostname = $"{name}.example.com", ipAddress = (string?)null, assignedIpAddresses = (string?)null, useSSL = true },
            module = ServerModules.Cwp7,
            username = "root",
            credentials = new { },
            limits = new { isDefault = false, isDisabled = false },
            hosting = new { monthlyCost = 1m },
            nameServers = new { },
        };

        (await client.PostAsJsonAsync("/api/admin/servers", body)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ServersTestSeeds.SearchServersAsync(client, name)).Single().GetProperty("endpoint")
            .GetProperty("acceptsSelfSignedCertificate").GetBoolean().Should().BeFalse();
    }

    /// <summary>A built in module id is stored in the host's own spelling whatever case the body used.</summary>
    /// <param name="sent">The module id as sent.</param>
    /// <param name="stored">The spelling the server is stored and listed with.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("cpanel", ServerModules.CPanel)]
    [InlineData("DIRECTADMIN", ServerModules.DirectAdmin)]
    [InlineData(ServerModules.Cwp7, ServerModules.Cwp7)]
    public async Task Module_BuiltInId_IsStoredInTheBuiltInSpellingAsync(string sent, string stored)
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";

        (await client.PostAsJsonAsync("/api/admin/servers", ServersTestSeeds.ServerBody(name, module: sent))).StatusCode
            .Should().Be(HttpStatusCode.Created);

        (await ServersTestSeeds.SearchServersAsync(client, name)).Single().GetProperty("module").GetString().Should().Be(stored);
    }

    /// <summary>A module neither the host nor an installed plugin knows is refused with <c>SERVER_MODULE_UNKNOWN</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Module_NobodyKnows_IsRefusedAsUnknownAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/servers", ServersTestSeeds.ServerBody($"Server-{Guid.NewGuid():N}", module: "no-such-panel"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SERVER_MODULE_UNKNOWN);
    }

    /// <summary>A module that is not a well formed id never reaches the handler.</summary>
    /// <param name="module">The module sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("not valid")]
    [InlineData("a/b")]
    public async Task Module_Malformed_IsRefusedByTheValidatorAsync(string module)
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/servers", ServersTestSeeds.ServerBody($"Server-{Guid.NewGuid():N}", module: module));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// A module an installed plugin declares is accepted and stored in the plugin's spelling, whatever case it was sent in, and the
    /// stored row carries the text id with no conversion.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Module_DeclaredByAnInstalledPlugin_IsStoredInThePluginsSpellingAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), serverModule: $"Hestia-{Guid.NewGuid():N}");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var catalog = new ServerModuleCatalog(harness.Registry);
        var handler = new CreateServerHandler(db, scope.ServiceProvider.GetRequiredService<IClock>(), catalog);
        var name = $"Server-{Guid.NewGuid():N}";

        var created = await handler.HandleAsync(
            new CreateServerCommand(
                name,
                new ServerEndpointDto($"{name}.example.com", null, null, true, false),
                harness.Module.ToUpperInvariant(),
                "root",
                new ServerCredentials(null, null, null),
                new ServerLimitsDto(null, false, false),
                new ServerHostingDto(null, null, 0m),
                ServerNameServers.None),
            CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        (await db.Servers.AsNoTracking().SingleAsync(s => s.Id == created.Value)).Module.Should().Be(harness.Module);
    }

    /// <summary>A server with an empty name is refused by the validator before it reaches the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnEmptyNameAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/servers", ServersTestSeeds.ServerBody(string.Empty, hostname: "empty-name.example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A body that leaves out one of the nested groups (endpoint, credentials, limits, hosting, name servers) is refused at binding.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesABodyWithoutItsNestedGroupsAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/servers", new
        {
            name = "Missing-groups",
            module = ServerModules.Cwp7,
            username = "root",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "create-server-forbidden");

        var response = await client.PostAsJsonAsync("/api/admin/servers", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/servers", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
