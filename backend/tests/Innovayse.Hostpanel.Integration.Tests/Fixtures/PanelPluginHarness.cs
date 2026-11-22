using System.Security.Cryptography;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// One hosting panel plugin that is installed, switched on and serving one unique module, with a stored server on that module and a
/// <see cref="FakePanelScript"/> to steer its answers: the real registry, activator, resolver and caller over the test container,
/// so a test drives the provider, the tester and the reader exactly as the host builds them.
/// </summary>
internal sealed class PanelPluginHarness : IDisposable
{
    /// <summary>The stored password of the seeded server, which a plugin must receive.</summary>
    public const string ServerPassword = "stored-panel-password";

    /// <summary>The scope the harness's database context and the plugin's services come from.</summary>
    private readonly IServiceScope _scope;

    /// <summary>The references the host gives plugins for a client, over a key of the test's own.</summary>
    private readonly PluginReferences _references = new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Initializes the harness from its parts.</summary>
    /// <param name="scope">The scope the harness's database context and the plugin's services come from.</param>
    /// <param name="pluginId">The installed plugin's id.</param>
    /// <param name="module">The module the plugin serves.</param>
    /// <param name="server">The stored server on that module.</param>
    /// <param name="script">The script the plugin follows.</param>
    /// <param name="registry">The registry the plugin is installed in.</param>
    /// <param name="activator">The activator over the test container.</param>
    private PanelPluginHarness(
        IServiceScope scope,
        string pluginId,
        string module,
        Server server,
        FakePanelScript script,
        PluginRegistry registry,
        PluginActivator activator)
    {
        _scope = scope;
        PluginId = pluginId;
        Module = module;
        Server = server;
        Script = script;
        Registry = registry;
        Activator = activator;
        Resolver = new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance);
        Caller = new PanelPluginCaller(registry, activator, Options.Create(new PluginApiOptions()), NullLogger<PanelPluginCaller>.Instance);
        Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>Gets the installed plugin's id.</summary>
    public string PluginId { get; }

    /// <summary>Gets the module the plugin serves: unique to this harness.</summary>
    public string Module { get; }

    /// <summary>Gets the stored server on <see cref="Module"/>, named uniquely so the plugin finds <see cref="Script"/>.</summary>
    public Server Server { get; }

    /// <summary>Gets the script the plugin follows.</summary>
    public FakePanelScript Script { get; }

    /// <summary>Gets the registry the plugin is installed in.</summary>
    public PluginRegistry Registry { get; }

    /// <summary>Gets the activator over the test container.</summary>
    public PluginActivator Activator { get; }

    /// <summary>Gets the resolver over <see cref="Registry"/>.</summary>
    public ServerModulePluginResolver Resolver { get; }

    /// <summary>Gets the caller over <see cref="Registry"/> and <see cref="Activator"/>.</summary>
    public PanelPluginCaller Caller { get; }

    /// <summary>Gets the database context the stored server lives in.</summary>
    public AppDbContext Db { get; }

    /// <summary>Gets the connection details the host's providers receive for the stored server.</summary>
    public ServerConnectionDto Connection => Server.ToConnection();

    /// <summary>Installs a plugin, optionally switches it on, and stores a server on its module.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="implementation">The CLR type that answers, <see cref="FakePanelPlugin"/> or <see cref="FakeLoginlessPanelPlugin"/>.</param>
    /// <param name="enabled">Whether the plugin is switched on.</param>
    /// <param name="serverModule">The module id to declare and store; unique when omitted.</param>
    /// <returns>The harness; dispose it when the test ends.</returns>
    public static async Task<PanelPluginHarness> CreateAsync(
        IntegrationTestFactory factory, Type implementation, bool enabled = true, string? serverModule = null)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pluginId = PluginActivationTestSeeds.NewId("panel");
        var module = serverModule ?? $"mod-{Guid.NewGuid():N}";
        string[] capabilities = typeof(IProvisioningPanelLoginPlugin).IsAssignableFrom(implementation)
            ? [PluginCapabilities.ProvisioningPanel, PluginCapabilities.ProvisioningPanelLogin]
            : [PluginCapabilities.ProvisioningPanel];
        var registry = PluginActivationTestSeeds.Registry(Plugin(pluginId, implementation, [module], capabilities));
        if (enabled)
        {
            await PluginActivationTestSeeds.EnableAsync(db, pluginId);
        }

        var server = Server.Create(
            new ServerDetails(
                $"srv-{Guid.NewGuid():N}", "panel.example.test", "203.0.113.9", null, module, "root",
                new ServerCredentials(ServerPassword, "api-token-value", "access-hash-value"), true, null, false, false, 0m, null, null,
                ServerNameServers.None,
                false),
            TestMoments.Seeded);
        db.Servers.Add(server);
        await db.SaveChangesAsync(CancellationToken.None);

        var script = FakePanelScript.Register(server.Name);
        var activator = PluginActivationTestSeeds.Activator(factory, db, scope.ServiceProvider, registry);
        return new PanelPluginHarness(scope, pluginId, module, server, script, registry, activator);
    }

    /// <summary>Builds a loaded provisioning plugin that declares capabilities and server modules, as if read from a validated manifest.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="modules">The <c>serverModules</c> the manifest lists.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin Plugin(string id, Type implementation, string[] modules, string[] capabilities)
    {
        var template = PluginActivationTestSeeds.Plugin(id, PluginType.Provisioning, implementation);
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Panel plugin",
            Version = template.Manifest.Version,
            Author = template.Manifest.Author,
            Description = template.Manifest.Description,
            Type = PluginType.Provisioning,
            Category = template.Manifest.Category,
            EntryPoint = template.Manifest.EntryPoint,
            SdkVersion = template.Manifest.SdkVersion,
            Capabilities = capabilities,
            ServerModules = modules,
        };

        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }

    /// <summary>Resolves a service from the scope the harness's database context and the plugin's services come from.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service.</returns>
    public T Service<T>()
        where T : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>Builds the provider the host's factory would hand out for the stored server.</summary>
    /// <returns>A provider that reaches the plugin through the real caller.</returns>
    public PanelPluginProvisioningProvider Provider()
    {
        return new PanelPluginProvisioningProvider(
            PluginId,
            Connection,
            Caller,
            new PanelConnectionReader(Db),
            _references,
            NullLogger<PanelPluginProvisioningProvider>.Instance);
    }

    /// <summary>Builds the host's provisioning factory over the harness's registry, activator and caller.</summary>
    /// <param name="options">The provisioning options; the defaults when omitted.</param>
    /// <param name="resolver">The resolver to use instead of the harness's own, or null.</param>
    /// <returns>The factory.</returns>
    public ProvisioningProviderFactory Factory(ProvisioningOptions? options = null, ServerModulePluginResolver? resolver = null)
    {
        return new ProvisioningProviderFactory(
            _scope.ServiceProvider.GetRequiredService<IHttpClientFactory>(),
            NullLoggerFactory.Instance,
            Options.Create(options ?? new ProvisioningOptions()),
            resolver ?? Resolver,
            Registry,
            Activator,
            Caller,
            new PanelConnectionReader(Db),
            _references);
    }

    /// <summary>Gives the plugin's reference for a client, the same way the provider does.</summary>
    /// <param name="clientId">The platform client id.</param>
    /// <returns>The opaque reference the plugin sees.</returns>
    public string ClientReference(int clientId)
    {
        return _references.ForClient(PluginId, clientId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _scope.Dispose();
    }
}
