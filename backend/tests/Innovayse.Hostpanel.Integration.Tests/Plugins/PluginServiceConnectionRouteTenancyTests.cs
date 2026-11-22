using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The server connection of a service, asked for through a real client route of a plugin of each type (#1357), against
/// Postgres and a real host: the plugin takes the service id from the path, so the route proves what the host answers when a
/// client names a service that is not its own. A client reaches only its own service the operator bound to the plugin, a
/// foreign service is refused exactly as a missing one, and a plugin without the <c>endpoints</c> capability has no route.
/// The host-side matrix of every refusal is in <see cref="ResolvePluginServiceConnectionTests"/>.
/// </summary>
public sealed class PluginServiceConnectionRouteTenancyTests : IClassFixture<PluginHostAccessFixture>
{
    /// <summary>The panel password the seeded servers store, which must never reach a response of a refused call.</summary>
    private const string StoredPassword = "route-password-5c2e";

    /// <summary>The API token the seeded servers store, which must never reach a response of a refused call.</summary>
    private const string StoredToken = "route-token-8d41";

    /// <summary>The access hash the seeded servers store, which must never reach a response of a refused call.</summary>
    private const string StoredHash = "route-hash-1fa9";

    /// <summary>The credentials every seeded server stores.</summary>
    private static readonly ServerCredentials Credentials = new(StoredPassword, StoredToken, StoredHash);

    /// <summary>The host with the serving plugins of every type.</summary>
    private readonly PluginHostAccessFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host with the serving plugins of every type.</param>
    public PluginServiceConnectionRouteTenancyTests(PluginHostAccessFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The address of the route that reads the server connection of a service.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="serviceId">The service id the caller puts in the path.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string pluginId, int serviceId)
    {
        return $"/api/client/plugins/{pluginId}/connection/{serviceId}";
    }

    /// <summary>Seeds an owner of an active service on a server that stores every credential, with a label unique to the plugin type.</summary>
    /// <param name="label">What the test is about.</param>
    /// <param name="type">The plugin type under test.</param>
    /// <returns>The owner, server and service.</returns>
    private Task<SshOwnerScenario> OwnerAsync(string label, PluginType type)
    {
        return _fixture.OwnerWithCredentialsAsync($"{label}-{type.ToString().ToLowerInvariant()}", Credentials);
    }

    /// <summary>Asserts that a refused answer carries no part of the server a caller must not see.</summary>
    /// <param name="response">The refused answer.</param>
    /// <param name="hostname">The host name of the server the caller asked about.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertNoServerDataAsync(HttpResponseMessage response, string hostname)
    {
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(hostname)
            .And.NotContain(StoredPassword)
            .And.NotContain(StoredToken)
            .And.NotContain(StoredHash);
    }

    /// <summary>A client asking for its own service, which the operator bound to the plugin, is handed that service's server.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task OwnBoundService_AnswersWithItsServer(PluginType type)
    {
        var pluginId = _fixture.ServingPluginIds[type];
        var owner = await OwnerAsync("route-own", type);
        await _fixture.BindProductsOfAsync(pluginId, PluginCapabilitySeeds.TabId, owner.ServiceId);
        using var http = AppPluginFixture.WithBearerOf(_fixture.Host, owner.Http);

        using var response = await http.GetAsync(Url(pluginId, owner.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("hostname").GetString().Should().Be($"route-own-{type.ToString().ToLowerInvariant()}.example.test");
        body.GetProperty("provisioningRef").GetString().Should().Be("web");
    }

    /// <summary>
    /// A client that puts another client's service id into the path, even a service bound to the same plugin, is refused
    /// exactly as for a service that does not exist, and the answer holds nothing of the other client's server.
    /// </summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task AnotherClientsService_IsRefusedAsAMissingOne(PluginType type)
    {
        var pluginId = _fixture.ServingPluginIds[type];
        var caller = await OwnerAsync("route-caller", type);
        var stranger = await OwnerAsync("route-stranger", type);
        await _fixture.BindProductsOfAsync(pluginId, PluginCapabilitySeeds.TabId, caller.ServiceId, stranger.ServiceId);
        using var http = AppPluginFixture.WithBearerOf(_fixture.Host, caller.Http);

        using var foreign = await http.GetAsync(Url(pluginId, stranger.ServiceId));
        using var missing = await http.GetAsync(Url(pluginId, int.MaxValue));

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginRefusalBodies.AssertIdenticalAsync(foreign, missing);
        await AssertNoServerDataAsync(foreign, $"route-stranger-{type.ToString().ToLowerInvariant()}.example.test");
    }

    /// <summary>A client's own service whose product the operator did not bind to the plugin is refused, with no server data.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task OwnServiceNotBoundByTheOperator_IsRefused(PluginType type)
    {
        var pluginId = _fixture.ServingPluginIds[type];
        var owner = await OwnerAsync("route-unbound", type);
        using var http = AppPluginFixture.WithBearerOf(_fixture.Host, owner.Http);

        using var response = await http.GetAsync(Url(pluginId, owner.ServiceId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AssertNoServerDataAsync(response, $"route-unbound-{type.ToString().ToLowerInvariant()}.example.test");
    }

    /// <summary>A caller with no sign-in is asked to sign in, and a staff account, which acts for no client, is refused by the portal policy.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task SignedOutAndStaffCallers_AreTurnedAwayAtTheDoor(PluginType type)
    {
        var pluginId = _fixture.ServingPluginIds[type];
        var owner = await OwnerAsync("route-door", type);
        await _fixture.BindProductsOfAsync(pluginId, PluginCapabilitySeeds.TabId, owner.ServiceId);
        using var anonymous = _fixture.Host.CreateClient();
        using var staff = AppPluginFixture.WithBearerOf(_fixture.Host, await _fixture.AdminClientAsync());

        using var signedOut = await anonymous.GetAsync(Url(pluginId, owner.ServiceId));
        using var staffCall = await staff.GetAsync(Url(pluginId, owner.ServiceId));

        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        staffCall.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A plugin that declares the same route and the access but not the <c>endpoints</c> capability has no route at all.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task PluginWithoutEndpoints_HasNoRouteAtTheDoor(PluginType type)
    {
        var owner = await OwnerAsync("route-plain", type);
        using var http = AppPluginFixture.WithBearerOf(_fixture.Host, owner.Http);

        using var plain = await http.GetAsync(Url(_fixture.PlainServingPluginIds[type], owner.ServiceId));
        using var unknown = await http.GetAsync(Url("never-loaded", owner.ServiceId));

        await PluginRefusalBodies.AssertSameAsync(plain, unknown, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
    }
}
