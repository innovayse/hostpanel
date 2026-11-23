using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// The stored integration settings, keyed by their full setting key and read through the shared key
/// builders of <see cref="IntegrationSettingKeys"/>. The list, detail and connection-test use cases
/// all answer "is it enabled" and "is this field filled in" from one of these, so the key
/// arithmetic, the case rules and the blank test are written once.
/// </summary>
public sealed class IntegrationSettingsLookup
{
    /// <summary>Full setting key to stored value, compared case-insensitively.</summary>
    private readonly Dictionary<string, string> _values;

    /// <summary>Initializes the lookup over already-filtered settings.</summary>
    /// <param name="values">Full setting key to stored value.</param>
    private IntegrationSettingsLookup(Dictionary<string, string> values)
    {
        _values = values;
    }

    /// <summary>Loads the settings whose key starts with a prefix.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="keyPrefix">
    /// The key prefix to keep: <see cref="IntegrationSettingKeys.RootPrefix"/> for every
    /// integration, or <see cref="IntegrationSettingKeys.Prefix(string)"/> for one.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lookup over the matching settings.</returns>
    public static async Task<IntegrationSettingsLookup> LoadAsync(
        IHostpanelDbContext context, string keyPrefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var all = await context.Settings.AsNoTracking().ToListAsync(ct);
        var values = all
            .Where(s => s.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);

        return new IntegrationSettingsLookup(values);
    }

    /// <summary>Tells whether an integration's enabled flag is on.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <returns><c>true</c> when the stored flag equals <see cref="IntegrationSettingKeys.EnabledValue"/>, ignoring case.</returns>
    public bool IsEnabled(string slug)
    {
        return _values.TryGetValue(IntegrationSettingKeys.EnabledKey(slug), out var value)
            && string.Equals(value, IntegrationSettingKeys.EnabledValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the stored value of one integration field. A secret field comes back as stored, that is
    /// encrypted: this lookup only answers presence for secrets.
    /// </summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="field">The field key.</param>
    /// <returns>The stored value, or <c>null</c> when nothing is stored.</returns>
    public string? GetValue(string slug, string field)
    {
        return _values.TryGetValue(IntegrationSettingKeys.FieldKey(slug, field), out var value) ? value : null;
    }

    /// <summary>Tells whether one integration field holds a non-blank value.</summary>
    /// <param name="slug">The integration slug.</param>
    /// <param name="field">The field key.</param>
    /// <returns><c>true</c> when a value is stored and is not blank.</returns>
    public bool HasValue(string slug, string field)
    {
        return !string.IsNullOrWhiteSpace(GetValue(slug, field));
    }
}
