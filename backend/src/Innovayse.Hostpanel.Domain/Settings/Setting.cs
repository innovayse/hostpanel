using System.Globalization;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>Represents a system-wide configuration key-value setting.</summary>
public sealed class Setting : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// The description a plugin's switch row is created with, <c>{0}</c> being the plugin id. The same sentence the
    /// integration save writes, so a row created by a removal reads like one created from the Integrations page.
    /// </summary>
    private const string PluginSwitchDescriptionFormat = "Whether the {0} integration is enabled.";

    /// <summary>Gets the unique configuration key.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the configuration value.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets the optional description of this setting.</summary>
    public string? Description { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Setting() { }

    /// <summary>
    /// Creates a new setting with the given key and value.
    /// </summary>
    /// <param name="key">The unique configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <param name="description">Optional description of this setting.</param>
    /// <returns>A new, unpersisted <see cref="Setting"/> instance.</returns>
    public static Setting Create(string key, string value, string? description) =>
        new() { Key = key, Value = value, Description = description };

    /// <summary>
    /// Creates the switch row of a removed plugin that never had one, already off, and raises
    /// <see cref="PluginRemovedEvent"/>. Its stored data is kept.
    /// </summary>
    /// <param name="pluginId">
    /// The removed plugin's id; must have the plugin id shape (<see cref="PluginIdentifier.IsWellFormed"/>).
    /// </param>
    /// <param name="removedAt">When the plugin was removed, UTC, read by the caller from its clock.</param>
    /// <returns>A new, unpersisted switch row holding <see cref="IntegrationSettingKeys.DisabledValue"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> breaks the plugin id shape.</exception>
    public static Setting CreateSwitchedOffForRemovedPlugin(string pluginId, DateTimeOffset removedAt)
    {
        EnsurePluginId(pluginId);

        var setting = Create(
            IntegrationSettingKeys.EnabledKey(pluginId),
            IntegrationSettingKeys.DisabledValue,
            string.Format(CultureInfo.InvariantCulture, PluginSwitchDescriptionFormat, pluginId));
        setting.AddDomainEvent(new PluginRemovedEvent(pluginId, removedAt));
        return setting;
    }

    // ── CRUD half ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the configuration value.
    /// </summary>
    /// <param name="value">The new value to store.</param>
    public void UpdateValue(string value) => Value = value;

    // ── specific logic ─────────────────────────────────────────────────────────

    /// <summary>
    /// Turns a removed plugin's switch off, so a plugin that stays loaded until the restart is refused from the next
    /// call on, and raises <see cref="PluginRemovedEvent"/>. The plugin's stored data is kept; <see cref="RequestDataPurge"/>
    /// deletes it.
    /// </summary>
    /// <param name="pluginId">
    /// The removed plugin's id; must have the plugin id shape (<see cref="PluginIdentifier.IsWellFormed"/>) and name this
    /// row's plugin.
    /// </param>
    /// <param name="removedAt">When the plugin was removed, UTC, read by the caller from its clock.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId"/> breaks the plugin id shape, or this row is not that plugin's switch.
    /// </exception>
    public void SwitchOffRemovedPlugin(string pluginId, DateTimeOffset removedAt)
    {
        EnsureSwitchOf(pluginId);
        Value = IntegrationSettingKeys.DisabledValue;
        AddDomainEvent(new PluginRemovedEvent(pluginId, removedAt));
    }

    /// <summary>
    /// Raises <see cref="PluginDataPurgeRequestedEvent"/> for the plugin this row switches, so the request and the
    /// purge of the plugin's stored data are one unit: the event is written to the outbox in the save that stores
    /// the request's audit line.
    /// </summary>
    /// <param name="pluginId">The plugin whose data is purged; must have the plugin id shape and name this row's plugin.</param>
    /// <param name="requestedAt">When the operator asked for the purge, UTC, read by the caller from its clock.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId"/> breaks the plugin id shape, or this row is not that plugin's switch.
    /// </exception>
    public void RequestDataPurge(string pluginId, DateTimeOffset requestedAt)
    {
        EnsureSwitchOf(pluginId);
        AddDomainEvent(new PluginDataPurgeRequestedEvent(pluginId, requestedAt));
    }

    /// <summary>Refuses a plugin id without the plugin id shape, or a row that is not that plugin's switch.</summary>
    /// <param name="pluginId">The candidate plugin id.</param>
    /// <exception cref="ArgumentException">The id breaks the plugin id shape, or this row is another setting.</exception>
    private void EnsureSwitchOf(string pluginId)
    {
        EnsurePluginId(pluginId);

        // Compared exactly: the keys are composed by IntegrationSettingKeys and a plugin id is lower case, so a row
        // whose key differs is another setting and must never be turned into a removal.
        if (!string.Equals(Key, IntegrationSettingKeys.EnabledKey(pluginId), StringComparison.Ordinal))
        {
            throw new ArgumentException("The setting is not the switch of this plugin.", nameof(pluginId));
        }
    }

    /// <summary>
    /// Refuses an id without the plugin id shape, before it can name a key or select rows. Only the shape: a plugin
    /// installed under an id that was reserved later must still be switched off and purged when it is removed.
    /// </summary>
    /// <param name="pluginId">The candidate plugin id.</param>
    /// <exception cref="ArgumentException"><paramref name="pluginId"/> breaks the plugin id shape.</exception>
    private static void EnsurePluginId(string pluginId)
    {
        if (!PluginIdentifier.IsWellFormed(pluginId))
        {
            throw new ArgumentException("The plugin id breaks the plugin id shape.", nameof(pluginId));
        }
    }
}
