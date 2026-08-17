namespace Innovayse.Application.Admin.Services;

using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Seeds the storefront's <c>portal.*</c> settings on a fresh install.
/// <para>
/// The portal reads these to decide which template to render and which contact
/// channels to offer, falling back to its own environment variables when a key is
/// absent. Without the rows the site still works, but an operator cannot change any
/// of it from the admin panel — <c>SettingsController</c> exposes update-by-id and
/// no create, so a key that was never seeded cannot be added there.
/// </para>
/// <para>
/// Like <c>DefaultDepartmentsSeeder</c> this runs in every environment, not only
/// Development: <c>DevDataSeeder</c> would leave a self-hosted install with nothing.
/// </para>
/// </summary>
public static class PortalSettingsSeeder
{
    /// <summary>
    /// The keys created on a fresh install, with the values a new install starts from.
    /// Every contact channel starts empty, which the portal renders as "hidden" rather
    /// than as a broken link — an operator fills in their own.
    /// </summary>
    private static readonly (string Key, string Value, string Description)[] _defaults =
    [
        ("portal.template",              "aurora", "Active storefront template: aurora or classic"),
        ("portal.contact.whatsapp",      "",       "WhatsApp number in international format, no leading +. Empty hides the action."),
        ("portal.contact.telegram",      "",       "Telegram handle without the @. Empty hides the action."),
        ("portal.chat.provider",         "",       "Live chat provider: chatwoot, or empty to disable the widget."),
        ("portal.newsletter.action_url", "",       "External newsletter form action URL. Empty hides the footer block."),
        ("portal.contact.email",         "",       "Public support address shown in the storefront footer. Empty hides it."),
        ("portal.social.facebook",        "",       "Facebook page URL. Empty hides the icon."),
        ("portal.social.instagram",       "",       "Instagram profile URL. Empty hides the icon."),
        ("portal.social.linkedin",        "",       "LinkedIn page URL. Empty hides the icon."),
        ("portal.social.youtube",         "",       "YouTube channel URL. Empty hides the icon."),
        ("portal.contact.phone",          "",       "Public phone number shown in the footer. Empty hides it."),
        ("portal.legal.tax_id",           "",       "Company tax identifier shown in the footer. Empty hides it."),
        ("portal.apps.enabled",           "false",  "Show the header app launcher. Off unless this deployment runs the apps it links to."),
        ("portal.apps.account",           "",       "URL for the Account entry in the header app launcher. Empty hides it."),
        ("portal.apps.tasks",             "",       "URL for the Tasks entry in the header app launcher. Empty hides it."),
        ("portal.apps.erp",               "",       "URL for the ERP entry in the header app launcher. Empty hides it."),
        ("portal.apps.hostpanel",         "",       "URL for the Hostpanel entry in the header app launcher. Empty hides it."),
        ("portal.apps.sheets",            "",       "URL for the Sheets entry in the header app launcher. Empty hides it."),
        ("portal.apps.mail",              "",       "URL for the Mail entry in the header app launcher. Empty hides it."),
        ("portal.apps.docs",              "",       "URL for the Docs entry in the header app launcher. Empty hides it."),
        ("portal.apps.calendar",          "",       "URL for the Calendar entry in the header app launcher. Empty hides it."),
    ];

    /// <summary>
    /// Creates any missing <c>portal.*</c> setting.
    /// <para>
    /// Keyed per setting rather than on the table being empty, unlike the department
    /// seeder: the settings table already carries unrelated rows from other seeding, so
    /// "is it empty" would never be true and none of these would ever be created. An
    /// existing key is left alone, so an operator's choice survives every restart.
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

        foreach (var (key, value, description) in _defaults)
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
