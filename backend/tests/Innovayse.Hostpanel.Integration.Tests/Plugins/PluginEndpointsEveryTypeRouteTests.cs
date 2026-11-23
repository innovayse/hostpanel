using System.Net;
using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The three route doors (client, admin and callback) with the <c>endpoints</c> capability, once for every plugin type that can
/// declare it (#818, #1357), against Postgres and a real host: a plugin that declares the capability and implements it answers
/// its declared routes, and the same plugin without the capability, or declaring it on a class that does not implement
/// <c>IPluginEndpoints</c>, answers every door with the very body an id that was never loaded gets.
/// </summary>
public sealed class PluginEndpointsEveryTypeRouteTests : IClassFixture<AppPluginFixture>
{
    /// <summary>A plugin id no host ever loads.</summary>
    private const string NeverLoaded = "never-loaded";

    /// <summary>The host, the database and the plugins the derived hosts start from.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the plugins the derived hosts start from.</param>
    public PluginEndpointsEveryTypeRouteTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The address of a client route of a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The declared path, with its leading slash.</param>
    /// <returns>The relative URL.</returns>
    private static string ClientUrl(string pluginId, string path) => $"/api/client/plugins/{pluginId}{path}";

    /// <summary>The address of the admin route of a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The relative URL.</returns>
    private static string AdminUrl(string pluginId) => $"/api/admin/plugins/{pluginId}/ext{PluginCapabilitySeeds.AdminPath}";

    /// <summary>The address of the callback route of a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The relative URL.</returns>
    private static string CallbackUrl(string pluginId) => $"/api/plugins/{pluginId}/callback{PluginCapabilitySeeds.CallbackPath}";

    /// <summary>Calls the client route, the admin route and the callback route of a plugin, each through its own door.</summary>
    /// <param name="client">An account holder.</param>
    /// <param name="admin">An administrator.</param>
    /// <param name="anonymous">A caller with no sign-in, which is what the callback door takes.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The three answers: client, admin, callback.</returns>
    private static async Task<IReadOnlyList<HttpResponseMessage>> CallEveryDoorAsync(
        HttpClient client, HttpClient admin, HttpClient anonymous, string pluginId)
    {
        return
        [
            await client.GetAsync(ClientUrl(pluginId, PluginCapabilitySeeds.ClientPath)),
            await admin.GetAsync(AdminUrl(pluginId)),
            await anonymous.PostAsync(CallbackUrl(pluginId), new StringContent("{}")),
        ];
    }

    /// <summary>A plugin of any type that declares the capability and implements it answers all three doors.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task ServingPlugin_AnswersItsClientAdminAndCallbackRoutes(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-route");
        using var client = AppPluginFixture.WithBearerOf(plugins.Host, owner.Client);
        using var admin = AppPluginFixture.WithBearerOf(plugins.Host, await _fixture.AdminClientAsync());
        using var anonymous = plugins.Host.CreateClient();

        var answers = await CallEveryDoorAsync(client, admin, anonymous, plugins.ServingId);

        answers.Select(answer => answer.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A plugin that does not declare the capability, and one that declares it on a class that does not implement it, answer
    /// every door with the body of an unknown plugin id.
    /// </summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task PluginThatDoesNotServe_AnswersTheUnknownPluginBodyOnEveryDoor(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-refused");
        using var client = AppPluginFixture.WithBearerOf(plugins.Host, owner.Client);
        using var admin = AppPluginFixture.WithBearerOf(plugins.Host, await _fixture.AdminClientAsync());
        using var anonymous = plugins.Host.CreateClient();
        var unknown = await CallEveryDoorAsync(client, admin, anonymous, NeverLoaded);

        foreach (var refusedId in plugins.RefusedIds)
        {
            var refused = await CallEveryDoorAsync(client, admin, anonymous, refusedId);

            for (var door = 0; door < refused.Count; door++)
            {
                await PluginRefusalBodies.AssertSameAsync(refused[door], unknown[door], HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
            }
        }
    }

    /// <summary>
    /// A contact who lacks the permission a route names is refused with the body of an unknown plugin id, while the account
    /// holder reaches the same route, for a plugin of any type.
    /// </summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task ContactWithoutThePermission_AnswersTheUnknownPluginBody(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-permission");
        var contact = await ClientsTestSeeds.LinkedToAsync(
            _fixture.Factory, "every-type-contact", owner.ClientId, ClientPermission.PlaceNewOrders);
        using var holder = AppPluginFixture.WithBearerOf(plugins.Host, owner.Client);
        using var limited = AppPluginFixture.WithBearerOf(plugins.Host, contact);

        using var allowed = await holder.GetAsync(ClientUrl(plugins.ServingId, PluginCapabilitySeeds.PermissionedPath));
        using var refused = await limited.GetAsync(ClientUrl(plugins.ServingId, PluginCapabilitySeeds.PermissionedPath));
        using var unknown = await limited.GetAsync(ClientUrl(NeverLoaded, PluginCapabilitySeeds.PermissionedPath));

        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await PluginRefusalBodies.AssertSameAsync(refused, unknown, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
    }
}
