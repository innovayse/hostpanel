using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UpdateSetting;

/// <summary>
/// Handles <see cref="UpdateSettingCommand"/> by loading the setting, checking the value
/// against the key's vocabulary where it has one, and persisting the change.
/// </summary>
/// <remarks>
/// The vocabulary check lives here and not in a FluentValidation validator because the rule
/// depends on the row's <c>Key</c>, which the command does not carry: a validator would have
/// to load the row this handler loads a moment later. The admin panel constrains
/// <c>portal.template</c> to a dropdown, but the raw settings table on the same page and the
/// API itself accept any string, and a value the storefront does not recognise makes it fall
/// back to its default with no sign of why the operator's choice was ignored.
/// </remarks>
public sealed class UpdateSettingHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Encrypts a secret value before it is stored.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Loaded plugins, whose manifests declare which of their fields are secret.</summary>
    private readonly IPluginRegistry _plugins;

    /// <summary>Removes the branding upload a replaced image setting pointed at.</summary>
    private readonly IBrandingStorage _branding;

    /// <summary>The acting admin, named in the audit line of a secret change.</summary>
    private readonly ICurrentUserService _currentUser;

    /// <summary>Stages the activity log line of a change to a setting that weakens a security control.</summary>
    private readonly IActivityLogger _activityLogger;

    /// <summary>Receives the audit line of a secret change (key only, never the value).</summary>
    private readonly ILogger<UpdateSettingHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cipher">Encrypts a secret value before it is stored.</param>
    /// <param name="plugins">Loaded plugins, whose manifests declare which of their fields are secret.</param>
    /// <param name="branding">Removes the branding upload a replaced image setting pointed at.</param>
    /// <param name="currentUser">The acting admin, named in the audit line of a secret change.</param>
    /// <param name="activityLogger">Stages the activity log line of a change to a setting that weakens a security control.</param>
    /// <param name="logger">Receives the audit line of a secret change.</param>
    public UpdateSettingHandler(
        IHostpanelDbContext context,
        ISecretSettingCipher cipher,
        IPluginRegistry plugins,
        IBrandingStorage branding,
        ICurrentUserService currentUser,
        IActivityLogger activityLogger,
        ILogger<UpdateSettingHandler> logger)
    {
        _context = context;
        _cipher = cipher;
        _plugins = plugins;
        _branding = branding;
        _currentUser = currentUser;
        _activityLogger = activityLogger;
        _logger = logger;
    }

    /// <summary>
    /// Loads the setting, applies the new value, and persists the change.
    /// </summary>
    /// <param name="command">The update setting command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>SETTING_NOT_FOUND</c> when the id names no setting, or
    /// <c>INVALID_SETTING_VALUE</c> when the key has a fixed vocabulary or shape and the value does
    /// not match it: <c>errors[key]</c> then carries what the key does accept.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateSettingCommand command, CancellationToken ct)
    {
        // Tracked, not AsNoTracking: the setting is mutated and saved further down.
        var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (setting is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SETTING_NOT_FOUND);
        }

        if (SettingSecrecy.From(_plugins).IsSecret(setting.Key))
        {
            return await UpdateSecretAsync(setting, command, ct);
        }

        // An explicit clear of a plain setting is the same as saving it empty.
        var requested = command.Clear ? string.Empty : command.Value;

        // Trimmed for every key: a trailing space typed into the admin table is never what an
        // operator meant, and for a URL or a handle it is the difference between working and not.
        var value = (requested ?? string.Empty).Trim();

        // Empty means "unset" for a key that seeds empty, the storefront then uses its
        // built-in default, and skips both checks below. A key that seeds with a value never
        // had an empty state and goes through them, where the empty string is refused.
        var allowsEmpty = PortalSettingKeys.AllowsEmpty(setting.Key) || BillingSettingKeys.AllowsEmpty(setting.Key);
        if (value.Length > 0 || !allowsEmpty)
        {
            if (PortalSettingKeys.AllowedValues.TryGetValue(setting.Key, out var allowed))
            {
                // The canonical spelling from the set is stored, not the caller's casing: the
                // storefront matches `portal.template` and the theme mode exactly, so "Aurora"
                // accepted as typed would still render the default.
                var canonical = allowed.FirstOrDefault(
                    a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
                if (canonical is null)
                {
                    var message =
                        $"Setting '{setting.Key}' does not accept '{value}'. Allowed: {string.Join(", ", allowed)}.";
                    return Invalid(setting.Key, message);
                }

                value = canonical;
            }
            else if (PortalSettingKeys.Patterns.TryGetValue(setting.Key, out var pattern)
                || BillingSettingKeys.Patterns.TryGetValue(setting.Key, out pattern)
                || PluginSettingKeys.Patterns.TryGetValue(setting.Key, out pattern))
            {
                // Stored in the canonical spelling so the storefront and the e-mail renderer
                // can compare and concatenate without normalising again.
                if (!pattern.Regex.IsMatch(value))
                {
                    var message =
                        $"Setting '{setting.Key}' does not accept '{value}'. Expected: {pattern.Expected}.";
                    return Invalid(setting.Key, message);
                }

                value = pattern.Canonical(value);
            }
        }

        var previous = setting.Value;
        setting.UpdateValue(value);

        // A setting that weakens a security control is audited on every change, in the same save as the
        // change itself, so the line and the new value commit or fail together.
        if (PluginSettingKeys.IsAudited(setting.Key) && !string.Equals(previous, value, StringComparison.Ordinal))
        {
            await _activityLogger.RecordAsync(
                new ActivityLogEntry(null, $"Changed Setting - Setting: {setting.Key} - From: {previous} - To: {value}"), ct);
        }

        await _context.SaveChangesAsync(ct);

        // A replaced branding image leaves its old upload referenced by nothing; it is removed once
        // the new value is saved, never before, so a failed save keeps the live files in place.
        if (PortalSettingKeys.BrandingImages.Contains(setting.Key)
            && previous.Length > 0
            && !string.Equals(previous, value, StringComparison.Ordinal))
        {
            await _branding.RemoveAsync(previous, ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Writes a secret setting: a clear empties it, a non-blank value replaces it, and an empty or
    /// absent value keeps what is stored. The admin panel never holds the stored value, so an
    /// empty field on save means "unchanged", never "erase".
    /// </summary>
    /// <param name="setting">The tracked secret setting.</param>
    /// <param name="command">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success; a secret has no vocabulary to refuse a value against.</returns>
    private async Task<Result> UpdateSecretAsync(Setting setting, UpdateSettingCommand command, CancellationToken ct)
    {
        SecretSettingChange change;
        if (command.Clear)
        {
            setting.UpdateValue(string.Empty);
            change = SecretSettingChange.Cleared;
        }
        else if (!string.IsNullOrWhiteSpace(command.Value))
        {
            setting.UpdateValue(_cipher.Protect(command.Value.Trim()));
            change = SecretSettingChange.Replaced;
        }
        else
        {
            return Result.Success();
        }

        await _context.SaveChangesAsync(ct);
        SecretSettingAudit.Record(_logger, setting.Key, change, _currentUser.UserId);
        return Result.Success();
    }

    /// <summary>
    /// Builds the <c>INVALID_SETTING_VALUE</c> refusal. <c>detail</c> in the response is the fixed
    /// sentence <c>ErrorMessages.resx</c> holds for the code; <paramref name="message"/> is the
    /// per-key wording that only this write knows, carried in <c>errors[key]</c> instead.
    /// </summary>
    /// <param name="key">The setting key that was written to.</param>
    /// <param name="message">What this key does accept, as a complete sentence.</param>
    /// <returns>The refusal.</returns>
    private static Result Invalid(string key, string message) =>
        Result.FailureWithDetail(message, HostpanelErrorCodes.INVALID_SETTING_VALUE, key, message);
}
