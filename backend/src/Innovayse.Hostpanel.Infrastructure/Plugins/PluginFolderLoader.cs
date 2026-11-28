using Hostpanel.Sdk.Packaging.Naming;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Checks one plugin folder and loads the plugin it holds into its own collectible <see cref="PluginLoadContext"/>, then
/// records it in the <see cref="PluginRegistry"/> and, when it exposes itself as an integration, in the
/// <see cref="PluginIntegrationRegistry"/>. The one place a folder becomes a loaded plugin, whether the host is starting
/// (<see cref="PluginLoader"/>) or a newer version is being swapped in (<see cref="PluginHotReloader"/>).
/// </summary>
/// <remarks>
/// An uploaded plugin is signature-checked before any of its assemblies is loaded, because loading an assembly can already
/// run its code. A plugin shipped with the host (<see cref="PluginOrigin.BuiltIn"/>) is not: it is part of the host. A
/// refused plugin is skipped alone and logged with its id and the rule it breaks; a plugin refused for a broken manifest
/// rule is also kept in the registry (<see cref="RefusedPlugin"/>), so the admin plugin list can show it. A plugin with a
/// pending upgrade record is flagged in the registry by the caller, which keeps it from being activated until its migration
/// succeeds.
/// </remarks>
internal sealed class PluginFolderLoader
{
    /// <summary>The longest load error kept for the admin list, in characters.</summary>
    private const int MaxLoadErrorLength = 300;

    /// <summary>The registry a loaded or refused plugin is recorded in.</summary>
    private readonly PluginRegistry _registry;

    /// <summary>The registry a plugin that shows in the admin integrations is recorded in.</summary>
    private readonly PluginIntegrationRegistry _integrations;

    /// <summary>Checks the signature of an uploaded plugin and applies the host's policy.</summary>
    private readonly PluginSignatureVerifier _verifier;

    /// <summary>Reads and checks a plugin's manifest.</summary>
    private readonly PluginManifestReader _reader;

    /// <summary>Where the outcome of a change to a plugin is recorded.</summary>
    private readonly PluginChangeStore _changes;

    /// <summary>Reads the key the registry published for the signer of an installed plugin, by plugin id.</summary>
    private readonly Func<string, PluginSignerKey?> _registryKeyOf;

    /// <summary>The upload folder, where the rollback copy of a replaced version waits.</summary>
    private readonly string _installRoot;

    /// <summary>The largest view document a plugin may ship, in bytes (<c>Plugins:MaxViewBytes</c>).</summary>
    private readonly long _maxViewBytes;

    /// <summary>Records what is skipped, refused and loaded.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes the loader.</summary>
    /// <param name="registry">The registry a loaded or refused plugin is recorded in.</param>
    /// <param name="integrations">The registry a plugin that shows in the admin integrations is recorded in.</param>
    /// <param name="verifier">Checks the signature of an uploaded plugin and applies the host's policy.</param>
    /// <param name="changes">Where the outcome of a change to a plugin is recorded.</param>
    /// <param name="registryKeyOf">Reads the key the registry published for the signer of an installed plugin; asked only when the configured keys do not know the signing key.</param>
    /// <param name="installRoot">The upload folder, where the rollback copy of a replaced version waits.</param>
    /// <param name="maxViewBytes">The largest view document a plugin may ship, in bytes.</param>
    /// <param name="loggerFactory">Creates the loggers of the loader and of the manifest reader.</param>
    public PluginFolderLoader(
        PluginRegistry registry,
        PluginIntegrationRegistry integrations,
        PluginSignatureVerifier verifier,
        PluginChangeStore changes,
        Func<string, PluginSignerKey?> registryKeyOf,
        string installRoot,
        long maxViewBytes,
        ILoggerFactory loggerFactory)
    {
        _registry = registry;
        _integrations = integrations;
        _verifier = verifier;
        _changes = changes;
        _registryKeyOf = registryKeyOf;
        _installRoot = installRoot;
        _maxViewBytes = maxViewBytes;
        _logger = loggerFactory.CreateLogger(nameof(PluginLoader));
        _reader = new PluginManifestReader(loggerFactory.CreateLogger<PluginManifestReader>());
    }

    /// <summary>
    /// Loads a plugin like <see cref="Load"/> and, when the folder holds a version that has just replaced another (the replaced
    /// version is kept in the rollback folder), settles the change: a version that loads is final and the kept one is
    /// deleted; a version that does not load is taken out again, the kept version is restored and loaded, and the failure is
    /// recorded with its reason and the restored version. The host keeps running either way, and the failed version is never
    /// tried again, so a bad plugin cannot cause a restart loop. A plugin that was installed fresh is settled the same way
    /// (<see cref="PluginFreshInstalls"/>): it has no earlier version, so a failed first load ends with the plugin taken out
    /// again, left uninstalled with no staged files, and the failure recorded with its reason.
    /// </summary>
    /// <param name="pluginDir">The plugin's folder; its name is the plugin id.</param>
    /// <param name="origin">Whether the plugin ships with the host or was uploaded.</param>
    /// <param name="claimedIds">The plugin ids earlier folders declared, loaded or not; this folder's id is added to it.</param>
    /// <param name="allowUnsigned">Whether the admin setting "allow unsigned plugins" is on.</param>
    /// <param name="reported">Whether the activity log already gets a line for the outcome from the caller, which is true for an in place reload.</param>
    /// <returns>The plugin as it is now in the registry (the restored version after a failure); <see langword="null"/> when none loaded.</returns>
    public LoadedPlugin? LoadChanged(string pluginDir, PluginOrigin origin, ISet<string> claimedIds, bool allowUnsigned, bool reported)
    {
        var id = Path.GetFileName(pluginDir);
        var due = origin == PluginOrigin.Uploaded
            && (Directory.Exists(Path.Combine(_installRoot, PluginFolders.Previous, id)) || PluginFreshInstalls.IsPending(_installRoot, id));
        var toVersion = due ? PluginUpgradeApplier.ReadVersion(pluginDir) : null;
        var fromVersion = due ? PluginUpgradeApplier.ReadVersion(Path.Combine(_installRoot, PluginFolders.Previous, id)) : null;

        var plugin = Load(pluginDir, origin, claimedIds, allowUnsigned);
        if (!due || toVersion is null)
        {
            return plugin;
        }

        PluginFreshInstalls.Clear(_installRoot, id);
        if (plugin is not null)
        {
            PluginUpgradeApplier.DiscardPrevious(_installRoot, id, _logger);
            _changes.Record(Change(id, fromVersion, toVersion, PluginChangeResult.Applied, null, null), reported);
            return plugin;
        }

        var error = _registry.GetRefusedPlugins().FirstOrDefault(r => r.PluginId == id)?.Detail
            ?? "the new version did not pass the load checks, see the log";
        var restored = PluginUpgradeApplier.RestorePrevious(_installRoot, id, _logger);
        _changes.Record(Change(id, fromVersion, toVersion, PluginChangeResult.Failed, error, restored), reported);

        // The failed version is gone, so the id is free again and its refusal no longer describes what is installed.
        claimedIds.Remove(id);
        _registry.ClearRefused(id);
        return restored is null ? null : Load(pluginDir, origin, claimedIds, allowUnsigned);
    }

    /// <summary>Builds the record of a change that ended now.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="from">The version that ran before the change.</param>
    /// <param name="to">The version the change brought.</param>
    /// <param name="result">Whether the new version loaded.</param>
    /// <param name="error">Why it did not load.</param>
    /// <param name="restored">The version that was restored after a failure.</param>
    /// <returns>The record.</returns>
    private static PluginChangeDto Change(string id, string? from, string to, PluginChangeResult result, string? error, string? restored)
    {
        return new PluginChangeDto(id, from, to, result, error, restored, TimeProvider.System.GetUtcNow());
    }

    /// <summary>Reads a plugin folder's manifest without loading anything, for a caller that decides whether to load the folder.</summary>
    /// <param name="pluginDir">The plugin's folder.</param>
    /// <returns>The manifest; <see langword="null"/> when the folder has none or it breaks a rule.</returns>
    public PluginManifest? ReadManifest(string pluginDir)
    {
        return _reader.TryRead(Path.Combine(pluginDir, PluginFolders.ManifestFileName), out _);
    }

    /// <summary>
    /// Loads the plugin in <paramref name="pluginDir"/>, when the folder is a plugin that passes every check. A folder
    /// that is not a plugin, one whose id an earlier folder already claimed and one that fails a check is skipped and logged.
    /// </summary>
    /// <param name="pluginDir">The plugin's folder; its name is the plugin id.</param>
    /// <param name="origin">Whether the plugin ships with the host or was uploaded.</param>
    /// <param name="claimedIds">The plugin ids earlier folders declared, loaded or not; this folder's id is added to it.</param>
    /// <param name="allowUnsigned">Whether the admin setting "allow unsigned plugins" is on.</param>
    /// <returns>The plugin as it is now in the registry; <see langword="null"/> when the folder did not load.</returns>
    public LoadedPlugin? Load(string pluginDir, PluginOrigin origin, ISet<string> claimedIds, bool allowUnsigned)
    {
        var folderName = Path.GetFileName(pluginDir);
        if (!PluginIdentifier.IsWellFormed(folderName))
        {
            return null;
        }

        // A well-formed folder under a reserved id is a plugin installed before the id was reserved: it is never
        // loaded (the host's own segment would shadow its routes), but it is reported, so the operator knows why it
        // is missing and removes it from the plugins page. A plain folder whose manifest declares a reserved id is
        // refused by the manifest reader (PluginManifestRule.PluginIdInvalid), the one place the manifest id is checked.
        if (PluginIdentifier.IsReserved(folderName))
        {
            _logger.LogWarning(
                "Plugin folder {Dir} uses the reserved plugin id {Id}. Skipping it; remove it from the plugins page.",
                pluginDir, folderName);
            return null;
        }

        var manifest = _reader.TryRead(Path.Combine(pluginDir, PluginFolders.ManifestFileName), out var violation);
        if (manifest is null)
        {
            RefuseUnreadableManifest(pluginDir, origin, claimedIds, violation);
            return null;
        }

        // Claimed by the first folder that declares it, loaded or not, so an upload can never
        // stand in for a built-in plugin that failed to load.
        if (!claimedIds.Add(manifest.Id))
        {
            _logger.LogWarning("Plugin {Id} in {Dir} is already declared by an earlier folder, skipping", manifest.Id, pluginDir);
            return null;
        }

        var refusal = _verifier.Check(pluginDir, manifest.Id, origin, allowUnsigned, _logger, registryKey: () => _registryKeyOf(manifest.Id)).Refusal;
        if (refusal is not null)
        {
            _logger.LogWarning(
                "Plugin {Id} refused by signature rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                manifest.Id, refusal.Rule, refusal.Detail);
            return null;
        }

        var views = PluginViewDocumentReader.Read(pluginDir, manifest, _maxViewBytes);
        if (views.Violation is not null)
        {
            LogManifestRuleRefusal(manifest.Id, views.Violation);
            return null;
        }

        var icon = PluginIconReader.Read(pluginDir, manifest);
        if (icon.Violation is not null)
        {
            LogManifestRuleRefusal(manifest.Id, icon.Violation);
            return null;
        }

        var loaded = LoadEntryPoint(pluginDir, manifest, origin);
        if (loaded is null)
        {
            return null;
        }

        var (context, pluginType) = loaded.Value;

        // A plugin that declares the endpoints capability promises the host it can answer routes. Declaring it without
        // implementing the contract is a broken manifest, refused here like any other, and kept in the registry so the
        // admin list shows why. The id is already claimed above, so an upload cannot stand in for it.
        if (manifest.Declares(PluginCapabilities.Endpoints) && !PluginCapabilityRules.IsImplementedBy(PluginCapabilities.Endpoints, pluginType))
        {
            var detail = $"capabilities declares '{PluginCapabilities.Endpoints}' but '{manifest.EntryPoint}' does not implement IPluginEndpoints";
            _logger.LogWarning(
                "Plugin {Id} refused by manifest rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
                manifest.Id, PluginManifestRule.EndpointsNotImplemented, detail);
            _registry.RegisterRefused(new RefusedPlugin(
                manifest.Id, PluginManifestRule.EndpointsNotImplemented, detail, origin == PluginOrigin.BuiltIn));
            context.Unload();
            return null;
        }

        // A rule added after the plugin was installed does not take it away: it was accepted once and may be
        // running payments. Logged only here, after the icon check and the assembly load, so a plugin skipped by a
        // later check never gets the line. The author sees the fault here and an upload of a new version is refused.
        foreach (var advisory in views.Advisories)
        {
            _logger.LogWarning(
                "Plugin {Id} breaks view rule {Rule}: {Detail}. It stays loaded; a new upload would be refused.",
                manifest.Id, advisory.Rule, advisory.Detail);
        }

        // Read last, like the advisories above, so a plugin skipped by an earlier check never gets a warning about its agent files.
        var agentProfile = PluginLoader.ReadAgentProfile(_registry, manifest, pluginDir, _logger);

        var plugin = new LoadedPlugin(manifest, pluginType, views.Views, icon.Icon, agentProfile, context);
        _registry.Register(plugin);

        if (manifest.ShowInIntegrations)
        {
            _integrations.Register(new PluginIntegrationEntry(
                Slug: manifest.Id,
                Name: manifest.Name,
                Category: manifest.IntegrationsCategory ?? manifest.Category,
                Type: manifest.Type,
                Color: manifest.Color,
                RequiredSettingKeys: manifest.GetSettings()
                    .Where(s => s.Required)
                    .Select(s => s.Key)
                    .ToList(),
                IconUrl: icon.Icon is null ? null : PluginIconPaths.For(manifest.Id, manifest.Version)));

            _logger.LogInformation(
                "Plugin {Id} registered as integration entry in category '{Category}'",
                manifest.Id,
                manifest.IntegrationsCategory ?? manifest.Category);
        }

        _logger.LogInformation("Plugin {Id} v{Version} loaded", manifest.Id, manifest.Version);
        return plugin;
    }

    /// <summary>
    /// Claims the id of a folder whose manifest cannot be read and keeps a broken manifest rule for the admin list.
    /// </summary>
    /// <param name="pluginDir">The plugin's folder.</param>
    /// <param name="origin">Where the folder is.</param>
    /// <param name="claimedIds">The plugin ids earlier folders declared.</param>
    /// <param name="violation">The manifest rule the folder breaks; <see langword="null"/> when the manifest is unreadable for another reason.</param>
    private void RefuseUnreadableManifest(string pluginDir, PluginOrigin origin, ISet<string> claimedIds, PluginManifestViolation? violation)
    {
        // A refused plugin claims its id like a loaded one, so an upload with the same name can never stand in for a
        // built-in plugin that was refused, and can never overwrite that plugin's refusal entry.
        var folderId = Path.GetFileName(pluginDir);
        if (!claimedIds.Add(folderId))
        {
            _logger.LogWarning("Plugin folder {Dir} is refused and its id {Id} is already declared by an earlier folder, skipping", pluginDir, folderId);
            return;
        }

        // Only a broken manifest rule is kept: the admin list shows its rule and detail, and the plugin
        // is never activated. Any other unreadable manifest stays a log line.
        if (violation is not null)
        {
            _registry.RegisterRefused(new RefusedPlugin(folderId, violation.Rule, violation.Detail, origin == PluginOrigin.BuiltIn));
        }
    }

    /// <summary>Logs that a plugin was skipped for a broken manifest rule.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="violation">The rule the plugin breaks.</param>
    private void LogManifestRuleRefusal(string pluginId, PluginManifestViolation violation)
    {
        _logger.LogWarning(
            "Plugin {Id} refused by manifest rule {Rule}: {Detail}. Skipping it; the other plugins still load.",
            pluginId, violation.Rule, violation.Detail);
    }

    /// <summary>
    /// Loads the plugin's assemblies into a context of their own and finds the entry point type in them. A plugin that
    /// cannot be loaded is kept in the registry with the reason, so it does not look healthy in the admin list.
    /// </summary>
    /// <param name="pluginDir">The plugin's folder.</param>
    /// <param name="manifest">The plugin's manifest, naming the entry point.</param>
    /// <param name="origin">Where the folder is.</param>
    /// <returns>The context and the entry point type; <see langword="null"/> when no assembly of the folder defines the entry point.</returns>
    private (PluginLoadContext Context, Type PluginType)? LoadEntryPoint(string pluginDir, PluginManifest manifest, PluginOrigin origin)
    {
        var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
        if (dllFiles.Length == 0)
        {
            _logger.LogError("No DLL found for plugin {Id} in {Dir}, skipping", manifest.Id, pluginDir);
            RefuseLoad(manifest.Id, origin, "the plugin folder holds no assembly");
            return null;
        }

        var context = new PluginLoadContext(manifest.Id, pluginDir);
        List<Exception> failures = [];
        foreach (var dll in dllFiles)
        {
            try
            {
                var assembly = context.LoadFromFile(dll);

                // throwOnError is on so a load that fails says why. With it off, GetType answers null for a type whose
                // dependency cannot be loaded (the plugin was built against an SDK version this host does not have)
                // exactly as for a type that is not there, and the log could only say "not found".
                var pluginType = assembly.GetType(manifest.EntryPoint, throwOnError: true);
                if (pluginType is not null)
                {
                    return (context, pluginType);
                }
            }
            catch (TypeLoadException ex)
            {
                // The usual case in a folder with helper assemblies: this one does not define the entry point. It is
                // logged below, with the skip, only when no assembly of the plugin defines it.
                failures.Add(ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load assembly {Dll} for plugin {Id}", dll, manifest.Id);
                failures.Add(ex);
            }
        }

        // Every assembly was tried, so each one that did not yield the entry point left its error. The one that
        // was asked for the entry point and could not give it is the cause; a helper assembly that failed to
        // load is only the fallback.
        var cause = failures.OfType<TypeLoadException>().FirstOrDefault() ?? failures[0];
        _logger.LogError(
            cause,
            "Entry point type '{EntryPoint}' not found for plugin {Id}, skipping",
            manifest.EntryPoint, manifest.Id);

        // The plugin is installed and its manifest was accepted, so it must not look healthy in the admin list:
        // the load error is kept beside it.
        RefuseLoad(manifest.Id, origin, FirstLine(cause.Message));
        context.Unload();
        return null;
    }

    /// <summary>
    /// Keeps an installed plugin that could not be loaded in the registry, so the admin list shows it with the reason.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="origin">Where the plugin folder is.</param>
    /// <param name="detail">Why the plugin did not load.</param>
    private void RefuseLoad(string pluginId, PluginOrigin origin, string detail)
    {
        _registry.RegisterRefused(new RefusedPlugin(pluginId, PluginManifestRule.AssemblyLoadFailed, detail, origin == PluginOrigin.BuiltIn));
    }

    /// <summary>
    /// Cuts a load error to its first line and a fixed length: the admin list shows what failed, never a stack.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <returns>The first line of the message, at most <see cref="MaxLoadErrorLength"/> characters.</returns>
    private static string FirstLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length <= MaxLoadErrorLength ? line : line[..MaxLoadErrorLength];
    }
}
