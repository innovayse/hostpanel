using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>Reads the licence key stored for a registry plugin, the one reader every registry call that sends it goes through.</summary>
public static class RegistryLicenceStore
{
    /// <summary>Reads the plain licence key of a plugin.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cipher">Decrypts the stored secret.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The key, or <see langword="null"/> when none is stored or the stored value is blank.</returns>
    public static async Task<string?> GetRegistryLicenceAsync(
        this IHostpanelDbContext context, ISecretSettingCipher cipher, string pluginId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cipher);

        var key = PluginSettingKeys.RegistryLicenceKey(pluginId);
        var stored = await context.Settings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        var plain = cipher.Reveal(stored);
        return string.IsNullOrWhiteSpace(plain) ? null : plain;
    }
}
