using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host with test-only app plugins loaded, one database and one probe, built once for a test class. The registry
/// holds an enabled plugin, a second enabled plugin with the same routes (for the per-plugin references), a plugin
/// that is registered but switched off, a plugin of another type that declares routes, and an enabled plugin whose
/// constructor throws. Tests script the answer through <see cref="Probe"/>, which a test resets first.
/// </summary>
public sealed class AppPluginFixture : IAsyncLifetime
{
    /// <summary>The body cap of the host, small so a test crosses it with a few bytes.</summary>
    public const int MaxBodyBytes = 64;

    /// <summary>Every log line the host wrote, for asserting that a failure was logged with the plugin id.</summary>
    private readonly RecordingLoggerProvider _logs = new();

    /// <summary>Gets the shared API and database the host is derived from.</summary>
    public IntegrationTestFactory Factory { get; } = new();

    /// <summary>Gets what the plugins recorded and how they answer.</summary>
    public AppPluginProbe Probe { get; } = new();

    /// <summary>Gets the host whose registry holds the plugins below.</summary>
    public WebApplicationFactory<Program> Host { get; private set; } = null!;

    /// <summary>Gets the id of the enabled plugin most tests call.</summary>
    public string PluginId { get; } = PluginActivationTestSeeds.NewId("app");

    /// <summary>Gets the id of a second enabled plugin that declares the same routes.</summary>
    public string OtherPluginId { get; } = PluginActivationTestSeeds.NewId("other");

    /// <summary>Gets the id of a plugin that is loaded but not switched on.</summary>
    public string DisabledPluginId { get; } = PluginActivationTestSeeds.NewId("off");

    /// <summary>Gets the id of an enabled plugin of a type that does not answer routes, though its manifest declares some.</summary>
    public string NotAppPluginId { get; } = PluginActivationTestSeeds.NewId("pay");

    /// <summary>Gets the id of an enabled app plugin whose constructor throws.</summary>
    public string BrokenPluginId { get; } = PluginActivationTestSeeds.NewId("broken");

    /// <summary>Builds the factory, the database and the derived host with the plugins registered and switched on.</summary>
    /// <returns>A task that completes when the host is ready.</returns>
    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        var registry = PluginActivationTestSeeds.Registry(
            Plugin(PluginId, PluginType.App, typeof(FakeAppPlugin)),
            Plugin(OtherPluginId, PluginType.App, typeof(FakeAppPlugin)),
            Plugin(DisabledPluginId, PluginType.App, typeof(FakeAppPlugin)),
            Plugin(NotAppPluginId, PluginType.Payment, typeof(FakeAppPlugin)),
            Plugin(BrokenPluginId, PluginType.App, typeof(ThrowingConstructorAppPlugin)));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PluginActivationTestSeeds.EnableAsync(db, PluginId, OtherPluginId, NotAppPluginId, BrokenPluginId);
        }

        Host = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PluginRegistry>();
                services.AddSingleton(registry);
                services.AddSingleton(Probe);
                services.Configure<PluginApiOptions>(options => options.MaxBodyBytes = MaxBodyBytes);
            });
        });
    }

    /// <summary>Releases the host and the factory.</summary>
    /// <returns>A task that completes when both are released.</returns>
    public async Task DisposeAsync()
    {
        Host.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>Makes a client of <see cref="Host"/> carrying the bearer token of an already signed-in client.</summary>
    /// <param name="signedIn">A client of another host whose bearer token is reused.</param>
    /// <returns>The client, to dispose when the test ends.</returns>
    public HttpClient ClientLike(HttpClient signedIn)
    {
        return WithBearerOf(Host, signedIn);
    }

    /// <summary>Makes a client of any host carrying the bearer token of an already signed-in client.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <param name="signedIn">A client whose bearer token is reused.</param>
    /// <returns>The client, to dispose when the test ends.</returns>
    public static HttpClient WithBearerOf(WebApplicationFactory<Program> host, HttpClient signedIn)
    {
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        return http;
    }

    /// <summary>Makes a client of <see cref="Host"/> carrying an administrator's bearer token.</summary>
    /// <returns>The client, to dispose when the test ends.</returns>
    public async Task<HttpClient> AdminClientAsync()
    {
        var http = Host.CreateClient();
        var token = await Factory.GetAdminTokenAsync();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>Derives a host from <see cref="Host"/> with more services replaced, for a caller the base host cannot serve.</summary>
    /// <param name="configure">Replaces services on top of those the base host registers.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    public WebApplicationFactory<Program> Derive(Action<IServiceCollection> configure)
    {
        return Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));
    }

    /// <summary>Whether the host logged a line at or above Warning that names a plugin.</summary>
    /// <param name="pluginId">The plugin id the line must name.</param>
    /// <returns><see langword="true"/> when such a line was logged.</returns>
    public bool LoggedFailureOf(string pluginId)
    {
        return _logs.Entries.Any(entry => entry.Level >= LogLevel.Warning && entry.Message.Contains(pluginId, StringComparison.Ordinal));
    }

    /// <summary>Reads the activity log lines that name a plugin, oldest first.</summary>
    /// <param name="pluginId">The plugin id the line must name.</param>
    /// <returns>The lines: the client they concern, if any, and the description.</returns>
    public async Task<IReadOnlyList<(int? ClientId, string Description)>> ActivityOfAsync(string pluginId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lines = await db.ActivityLogs.AsNoTracking()
            .Where(line => line.Description.Contains(pluginId))
            .OrderBy(line => line.Id)
            .Select(line => new { line.ClientId, line.Description })
            .ToListAsync();

        return lines.Select(line => (line.ClientId, line.Description)).ToList();
    }

    /// <summary>One declared route.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path template.</param>
    /// <param name="permission">The permission, <see langword="null"/> for none.</param>
    /// <param name="streaming">Whether the route streams its reply.</param>
    /// <returns>The route.</returns>
    private static PluginRoute Route(PluginRouteMethod method, string path, string? permission, bool streaming = false)
    {
        return new PluginRoute { Method = method, Path = path, Permission = permission, Streaming = streaming };
    }

    /// <summary>A loaded plugin whose manifest declares the standard test routes.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Plugin(string id, PluginType type, Type implementation)
    {
        var loaded = PluginActivationTestSeeds.Plugin(id, type, implementation);
        var manifest = new PluginManifest
        {
            Id = loaded.Manifest.Id,
            Name = loaded.Manifest.Name,
            Version = loaded.Manifest.Version,
            Author = loaded.Manifest.Author,
            Description = loaded.Manifest.Description,
            Type = type,
            Category = loaded.Manifest.Category,
            EntryPoint = loaded.Manifest.EntryPoint,
            SdkVersion = loaded.Manifest.SdkVersion,
            Api = new PluginApi
            {
                Client =
                [
                    Route(PluginRouteMethod.Get, "/ping", PluginPermissions.Any),
                    Route(PluginRouteMethod.Get, "/invoices/{id}", nameof(ClientPermission.ViewPayInvoices)),
                    Route(PluginRouteMethod.Post, "/invoices/{id}/pay", nameof(ClientPermission.ViewPayInvoices)),
                    Route(PluginRouteMethod.Get, "/orders", nameof(ClientPermission.PlaceNewOrders)),
                    Route(PluginRouteMethod.Post, "/notes", null),
                    Route(PluginRouteMethod.Post, "/echo", PluginPermissions.Any),
                    Route(PluginRouteMethod.Get, "/items/latest", PluginPermissions.Any),
                    Route(PluginRouteMethod.Get, "/items/{name}", PluginPermissions.Any),
                    Route(PluginRouteMethod.Get, "/stream", PluginPermissions.Any, streaming: true),
                ],
                Admin =
                [
                    Route(PluginRouteMethod.Get, "/stats", null),
                    Route(PluginRouteMethod.Post, "/reindex", null),
                    Route(PluginRouteMethod.Post, "/echo", null),
                    Route(PluginRouteMethod.Get, "/stream", null, streaming: true),
                ],
            },
        };

        return new LoadedPlugin(manifest, implementation);
    }
}
