using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

/// <summary>
/// Handles <see cref="SaveIntegrationConfigCommand"/> by upserting each field as a key-value
/// <see cref="Setting"/> and persisting in one transaction.
/// </summary>
public sealed class SaveIntegrationConfigHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    /// Invalidates the cached hosting-panel server-info results a saved credential change would make
    /// stale.
    /// </summary>
    private readonly IMemoryCache _cache;

    /// <summary>Encrypts a secret field value before it is stored.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Loaded plugins, whose manifests declare their fields and which of them are secret.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>The acting admin, named in the audit line of a secret change.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Runs the pending migration of an upgraded plugin once the operator has switched it on and saved its settings.</summary>
    private readonly IPluginMigrationRetry _migrations;

    /// <summary>Receives the audit line of a secret change (key only, never the value).</summary>
    private readonly ILogger<SaveIntegrationConfigHandler> _logger;

    /// <summary>Drops the panels' remembered plugin availability once the switch and fields have committed.</summary>
    private readonly IPluginAvailabilityInvalidator _availability;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cache">
    /// Invalidates the cached hosting-panel server-info results a saved credential change would make
    /// stale.
    /// </param>
    /// <param name="cipher">Encrypts a secret field value before it is stored.</param>
    /// <param name="plugins">Loaded plugins, whose manifests declare their fields and which of them are secret.</param>
    /// <param name="currentUser">The acting admin, named in the audit line of a secret change.</param>
    /// <param name="migrations">Runs the pending migration of an upgraded plugin once the operator has switched it on and saved its settings.</param>
    /// <param name="logger">Receives the audit line of a secret change.</param>
    /// <param name="availability">Drops the panels' remembered plugin availability once the switch and fields have committed.</param>
    public SaveIntegrationConfigHandler(
        IHostpanelDbContext context,
        IMemoryCache cache,
        ISecretSettingCipher cipher,
        IPluginRegistry plugins,
        ICurrentUserService currentUser,
        IPluginMigrationRetry migrations,
        ILogger<SaveIntegrationConfigHandler> logger,
        IPluginAvailabilityInvalidator availability)
    {
        _migrations = migrations;
        _context = context;
        _cache = cache;
        _cipher = cipher;
        _plugins = plugins;
        _currentUser = currentUser;
        _logger = logger;
        _availability = availability;
    }

    /// <summary>
    /// Upserts the is_enabled flag and every supplied field for the given slug, then saves all
    /// changes in a single call.
    /// </summary>
    /// <param name="command">The save integration config command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INTEGRATION_NOT_FOUND</c> when the slug names neither a built-in integration
    /// nor a loaded plugin.
    /// </returns>
    public async Task<Result> HandleAsync(SaveIntegrationConfigCommand command, CancellationToken ct)
    {
        var slug = command.Slug;
        if (IntegrationFields.Resolve(slug, _plugins) is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INTEGRATION_NOT_FOUND);
        }

        // Upsert the is_enabled flag.
        await UpsertAsync(
            IntegrationSettingKeys.EnabledKey(slug),
            command.IsEnabled ? IntegrationSettingKeys.EnabledValue : IntegrationSettingKeys.DisabledValue,
            $"Whether the {slug} integration is enabled.",
            ct);

        var secrecy = SettingSecrecy.From(_plugins);
        var clear = new HashSet<string>(command.ClearFields ?? [], StringComparer.OrdinalIgnoreCase);
        var audit = new List<(string Key, SecretSettingChange Change)>();

        foreach (var (fieldKey, fieldValue) in command.Config)
        {
            if (clear.Contains(fieldKey))
            {
                continue; // Cleared below; a clear wins over a value sent alongside it.
            }

            var key = IntegrationSettingKeys.FieldKey(slug, fieldKey);
            var isSecret = secrecy.IsSecret(key);

            // The admin panel never receives a secret, so an empty secret field means "unchanged".
            if (isSecret && string.IsNullOrWhiteSpace(fieldValue))
            {
                continue;
            }

            // A secret is stored encrypted; the readers that need it decrypt through the same cipher.
            var stored = isSecret ? _cipher.Protect(fieldValue!) : fieldValue ?? string.Empty;
            await UpsertAsync(key, stored, $"{slug} integration field: {fieldKey}.", ct);
            if (isSecret)
            {
                audit.Add((key, SecretSettingChange.Replaced));
            }
        }

        foreach (var fieldKey in clear)
        {
            var key = IntegrationSettingKeys.FieldKey(slug, fieldKey);
            var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing is null || existing.Value.Length == 0)
            {
                continue;
            }

            existing.UpdateValue(string.Empty);
            if (secrecy.IsSecret(key))
            {
                audit.Add((key, SecretSettingChange.Cleared));
            }
        }

        await _context.SaveChangesAsync(ct);

        // The switch and the required fields decide whether a plugin's pages show; after the commit, the panels read
        // them again at once instead of after the cache lifetime.
        _availability.Invalidate();

        foreach (var (key, change) in audit)
        {
            SecretSettingAudit.Record(_logger, key, change, _currentUser.UserId);
        }

        // An upgraded plugin whose migration could not run (switched off, or a required setting missing) can run it now;
        // a no-op for every other slug. A failure is kept inside the port: the settings are already saved.
        if (command.IsEnabled)
        {
            await _migrations.RetryAsync(slug, ct);
        }

        // A saved hosting-panel credential invalidates the cached live server-info result the
        // read side serves for 5 minutes, so the operator sees the new credentials take effect
        // immediately rather than reading the old server's status until the cache expires.
        if (string.Equals(slug, IntegrationSlugs.Cwp, StringComparison.OrdinalIgnoreCase))
        {
            _cache.Remove(HostingPanelCacheKeys.Cwp);
        }

        return Result.Success();
    }

    /// <summary>
    /// Finds an existing setting by <paramref name="key"/> and updates its value, or creates a new
    /// setting when none exists.
    /// </summary>
    /// <param name="key">The full setting key, e.g. <c>integration:{slug}:secret_key</c>.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="description">Human-readable description stored when creating a new setting.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous upsert operation.</returns>
    private async Task UpsertAsync(string key, string value, string description, CancellationToken ct)
    {
        var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing is null)
        {
            _context.Settings.Add(Setting.Create(key, value, description));
        }
        else
        {
            existing.UpdateValue(value);
        }
    }
}
