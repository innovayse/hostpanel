using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Seeding;

/// <summary>
/// Seeds the storefront's <c>portal.*</c> settings, the billing engine's <c>billing.renewal.*</c> settings and the plugin system's <c>plugins.*</c> settings, on a fresh install.
/// <para>
/// The portal reads these to decide which template to render, which colour mode to
/// start in, which logo to show and which contact channels to offer, falling back to
/// its own environment variables when a key is absent. Without the rows the site still
/// works, but an operator cannot change any of it from the admin panel:
/// <c>SettingsController</c> exposes update-by-id and no create, so a key that was
/// never seeded cannot be added there.
/// </para>
/// <para>
/// The key list itself is <see cref="PortalSettingKeys.Defaults"/>, shared with the
/// public allow-list so the two cannot drift apart.
/// </para>
/// <para>
/// Like <c>DefaultDepartmentsSeeder</c> this runs in every environment, not only
/// Development: <c>DevDataSeeder</c> would leave a self-hosted install with nothing.
/// </para>
/// </summary>
public static class PortalSettingsSeeder
{
    /// <summary>
    /// Creates any missing <c>portal.*</c> setting.
    /// <para>
    /// Keyed per setting rather than on the table being empty, unlike the department
    /// seeder: the settings table already carries unrelated rows from other seeding, so
    /// "is it empty" would never be true and none of these would ever be created. An
    /// existing key is left alone, so an operator's choice survives every restart, and
    /// a key introduced by a later release is added to an existing install on its next
    /// start, which is how an upgrade gains a new admin control without a migration.
    /// </para>
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of settings created.</returns>
    public static async Task<int> EnsureSeededAsync(
        IHostpanelDbContext context,
        CancellationToken ct = default)
    {
        var created = 0;

        // Billing and plugin keys ride the same seeder so both startup and the demo reset (the two callers) seed them.
        var defaults = PortalSettingKeys.Defaults.Concat(BillingSettingKeys.Defaults).Concat(PluginSettingKeys.Defaults).ToList();
        foreach (var (key, value, description) in defaults)
        {
            // Tracked, not AsNoTracking: a newly created
            // row is added and saved below, and this same query must see an uncommitted add from
            // an earlier iteration were the key ever repeated.
            var existing = await context.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing is not null)
            {
                continue;
            }

            context.Settings.Add(Setting.Create(key, value, description));
            created++;
        }

        if (created == 0)
        {
            return 0;
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on the key: two instances starting together both found the key missing
            // and the other one inserted first. Our rejected rows are dropped from the tracker, and
            // the start carries on when every key is there now; a key still missing is a real failure.
            context.DetachAll();
            var keys = defaults.Select(d => d.Key).ToList();
            var present = await context.Settings.AsNoTracking().CountAsync(s => keys.Contains(s.Key), ct);
            if (present < keys.Count)
            {
                throw;
            }

            return 0;
        }

        return created;
    }
}
