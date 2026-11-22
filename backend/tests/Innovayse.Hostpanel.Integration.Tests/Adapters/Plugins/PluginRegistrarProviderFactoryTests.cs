using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistrarProviderFactory"/> against Postgres: a registrar module is the id of a plugin, found without regard
/// to letter case, and a plugin that is not installed, not a registrar, switched off, missing a required setting or without the
/// operations capability resolves to a provider that refuses with <c>REGISTRAR_NOT_CONFIGURED</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginRegistrarProviderFactoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The domain the tests address, to tell a refusing provider from a working one.</summary>
    private static readonly DomainRef Domain = new("example.test", "REF-EXAMPLE");

    /// <summary>Builds the factory over a registry holding the given plugins.</summary>
    /// <param name="db">The data context the activator reads the settings from.</param>
    /// <param name="plugins">The plugins the registry holds.</param>
    /// <returns>The factory under test.</returns>
    private PluginRegistrarProviderFactory Factory(AppDbContext db, params LoadedPlugin[] plugins)
    {
        var registry = PluginActivationTestSeeds.Registry(plugins);

        return new PluginRegistrarProviderFactory(
            registry,
            PluginActivationTestSeeds.Activator(factory, db, registry: registry),
            NullLogger<PluginRegistrarProviderFactory>.Instance,
            NullLogger<PluginRegistrarProvider>.Instance);
    }

    /// <summary>Stores the settings that make a registrar from <see cref="RegistrarRegistryTestSeeds"/> configured and switches it on.</summary>
    /// <param name="db">The data context.</param>
    /// <param name="id">The plugin id.</param>
    /// <returns>A task that completes when the settings are stored.</returns>
    private static async Task ConfigureAsync(AppDbContext db, string id)
    {
        await PluginActivationTestSeeds.EnableAsync(db, id);
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.AccountField, "account-1");
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.KeyField, "key-1");
    }

    /// <summary>Asserts that the provider refuses an operation as an unconfigured registrar does.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    private static async Task AssertRefusesAsync(IRegistrarProvider provider)
    {
        var result = await provider.CancelTransferAsync(Domain, CancellationToken.None);

        result.Should().Be(new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED));
    }

    /// <summary>An enabled, configured registrar plugin resolves to the adapter, and the plugin is called with the connection built from its settings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Activated_ResolvesToTheAdapterWithTheConnectionBuiltFromItsSettingsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("reg");
        await ConfigureAsync(db, id);

        var provider = await Factory(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).GetProviderAsync(id, CancellationToken.None);

        provider.Should().BeOfType<PluginRegistrarProvider>();
        var pricing = (await provider.GetTldPricingAsync(CancellationToken.None)).Value!;
        pricing.Should().ContainSingle().Which.Tld.Should().Be("account-1");
        pricing[0].Currency.Should().Be("key-1");
    }

    /// <summary>A stored module in another letter case than the plugin id still finds the plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ModuleInAnotherLetterCase_FindsThePluginAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("legacy");
        await ConfigureAsync(db, id);

        var provider = await Factory(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).GetProviderAsync(id.ToUpperInvariant(), CancellationToken.None);

        provider.Should().BeOfType<PluginRegistrarProvider>();
    }

    /// <summary>No module, a module no plugin has and a plugin of another type all resolve to the refusing provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NoModuleUnknownModuleAndWrongType_ResolveToTheRefusingProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wrongTypeId = PluginActivationTestSeeds.NewId("pay");
        await ConfigureAsync(db, wrongTypeId);
        var subject = Factory(db, RegistrarRegistryTestSeeds.Plugin(wrongTypeId, PluginType.Payment, typeof(ScriptedRegistrarPlugin)));

        await AssertRefusesAsync(await subject.GetProviderAsync(null, CancellationToken.None));
        await AssertRefusesAsync(await subject.GetProviderAsync("no-such-registrar", CancellationToken.None));
        await AssertRefusesAsync(await subject.GetProviderAsync(wrongTypeId, CancellationToken.None));
    }

    /// <summary>A registrar plugin the admin did not switch on resolves to the refusing provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Disabled_ResolvesToTheRefusingProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("off");
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.AccountField, "account-1");
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.KeyField, "key-1");

        await AssertRefusesAsync(await Factory(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).GetProviderAsync(id, CancellationToken.None));
    }

    /// <summary>A switched-on registrar missing a required setting resolves to the refusing provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RequiredSettingMissing_ResolvesToTheRefusingProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("half");
        await PluginActivationTestSeeds.EnableAsync(db, id);
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.AccountField, "account-1");

        await AssertRefusesAsync(await Factory(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).GetProviderAsync(id, CancellationToken.None));
    }

    /// <summary>A registrar plugin whose manifest declares no operations capability (a marker only plugin) resolves to the refusing provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithoutTheOperationsCapability_ResolvesToTheRefusingProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("marker");
        await ConfigureAsync(db, id);
        var markerOnly = RegistrarRegistryTestSeeds.Plugin(id, PluginType.Registrar, typeof(ScriptedRegistrarPlugin));

        await AssertRefusesAsync(await Factory(db, markerOnly).GetProviderAsync(id, CancellationToken.None));
    }

    /// <summary>A capability the manifest declares but the class does not implement refuses the plugin for every contract.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DeclaredCapabilityNotImplemented_ResolvesToTheRefusingProviderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("liar");
        await ConfigureAsync(db, id);
        var declaresWhois = RegistrarRegistryTestSeeds.Plugin(
            id, PluginType.Registrar, typeof(ScriptedRegistrarPlugin), PluginCapabilities.RegistrarOperations, PluginCapabilities.RegistrarWhois);

        await AssertRefusesAsync(await Factory(db, declaresWhois).GetProviderAsync(id, CancellationToken.None));
    }
}
