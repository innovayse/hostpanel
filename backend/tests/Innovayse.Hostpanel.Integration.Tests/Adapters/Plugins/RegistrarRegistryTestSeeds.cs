using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Builds loaded registrar plugins whose manifests carry the settings and connection roles a test needs.</summary>
internal static class RegistrarRegistryTestSeeds
{
    /// <summary>The setting key that plays the account name role in every seeded manifest.</summary>
    public const string AccountField = "login_name";

    /// <summary>The setting key that plays the API key role in every seeded manifest.</summary>
    public const string KeyField = "secret_token";

    /// <summary>The setting key that plays the test mode role in every seeded manifest.</summary>
    public const string TestModeField = "sandbox";

    /// <summary>Builds the manifest settings of a registrar: an account name, an API key (required) and a test mode switch.</summary>
    /// <returns>The three settings, each with its connection role.</returns>
    public static PluginSetting[] Settings()
    {
        return
        [
            new PluginSetting { Key = AccountField, Label = "Account", Type = PluginSettingType.Text, Required = true, ConnectionRole = RegistrarConnectionRole.AccountName },
            new PluginSetting { Key = KeyField, Label = "Key", Type = PluginSettingType.Secret, Required = true, ConnectionRole = RegistrarConnectionRole.ApiKey },
            new PluginSetting { Key = TestModeField, Label = "Sandbox", Type = PluginSettingType.Boolean, ConnectionRole = RegistrarConnectionRole.TestMode },
        ];
    }

    /// <summary>Builds a loaded plugin of the given type with the registrar settings and capabilities.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The manifest type.</param>
    /// <param name="implementation">The implementation type.</param>
    /// <param name="capabilities">The capabilities the manifest declares.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin Plugin(string id, PluginType type, Type implementation, params string[] capabilities)
    {
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Test registrar",
            Version = "1.0.0",
            Author = "Tests",
            Description = "Registrar plugin used by the registrar tests.",
            Type = type,
            Category = type.ToString(),
            EntryPoint = implementation.FullName!,
            SdkVersion = "3.11.0",
            ManifestVersion = 2,
            Settings = Settings(),
            Capabilities = capabilities,
        };

        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }

    /// <summary>Builds a registrar plugin that implements every optional contract and declares all of its capabilities.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin FullRegistrar(string id)
    {
        return Plugin(
            id,
            PluginType.Registrar,
            typeof(FullRegistrarPlugin),
            PluginCapabilities.RegistrarOperations,
            PluginCapabilities.RegistrarDnsManagement,
            PluginCapabilities.RegistrarEmailForwarding,
            PluginCapabilities.RegistrarWhois);
    }

    /// <summary>Builds a registrar plugin that implements and declares the operations contract only.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin OperationsOnlyRegistrar(string id)
    {
        return Plugin(id, PluginType.Registrar, typeof(ScriptedRegistrarPlugin), PluginCapabilities.RegistrarOperations);
    }
}
