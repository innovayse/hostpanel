namespace Innovayse.Application.Admin.Services;

using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Seeds the storefront's <c>portal.*</c> settings on a fresh install.
/// <para>
/// The portal reads these to decide which template to render, which colour mode to
/// start in, which logo to show and which contact channels to offer, falling back to
/// its own environment variables when a key is absent. Without the rows the site still
/// works, but an operator cannot change any of it from the admin panel —
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
    /// existing key is left alone, so an operator's choice survives every restart — and
    /// a key introduced by a later release is added to an existing install on its next
    /// start, which is how an upgrade gains a new admin control without a migration.
    /// </para>
    /// </summary>
    /// <param name="settings">Setting repository.</param>
    /// <param name="uow">Unit of work for persisting the new settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of settings created.</returns>
    public static async Task<int> EnsureSeededAsync(
        ISettingRepository settings,
        IUnitOfWork uow,
        CancellationToken ct = default)
    {
        var created = 0;

        foreach (var (key, value, description) in PortalSettingKeys.Defaults)
        {
            var existing = await settings.FindByKeyAsync(key, ct);
            if (existing is not null)
            {
                continue;
            }

            settings.Add(Setting.Create(key, value, description));
            created++;
        }

        if (created > 0)
        {
            await uow.SaveChangesAsync(ct);
        }

        return created;
    }
}
