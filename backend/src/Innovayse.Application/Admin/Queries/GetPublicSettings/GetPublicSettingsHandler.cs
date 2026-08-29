namespace Innovayse.Application.Admin.Queries.GetPublicSettings;

using Innovayse.Application.Admin.DTOs;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetPublicSettingsQuery"/> by returning only the settings the
/// public storefront needs.
/// </summary>
/// <param name="repo">Setting repository.</param>
public sealed class GetPublicSettingsHandler(ISettingRepository repo)
{
    /// <summary>
    /// The only keys this handler will ever return.
    /// <para>
    /// An allow-list rather than a prefix match on purpose. A prefix would silently
    /// start exposing anything a future contributor happens to name <c>portal.*</c>,
    /// and this response is served without authentication to a table that also holds
    /// integration credentials.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> _publicKeys =
    [
        "portal.template",
        "portal.logo",
        "portal.favicon",
        "portal.contact.whatsapp",
        "portal.contact.telegram",
        "portal.chat.provider",
        "portal.newsletter.action_url",
        "portal.contact.email",
        "portal.social.facebook",
        "portal.social.instagram",
        "portal.social.linkedin",
        "portal.social.youtube",
        "portal.contact.phone",
        "portal.legal.tax_id",
        "portal.apps.enabled",
        "portal.apps.account",
        "portal.apps.tasks",
        "portal.apps.erp",
        "portal.apps.hostpanel",
        "portal.apps.sheets",
        "portal.apps.mail",
        "portal.apps.docs",
        "portal.apps.calendar",
    ];

    /// <summary>
    /// Returns the allow-listed settings that currently have a value.
    /// </summary>
    /// <param name="query">The get public settings query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of key/value pairs; empty when none are set.</returns>
    public async Task<IReadOnlyList<PublicSettingDto>> HandleAsync(
        GetPublicSettingsQuery query,
        CancellationToken ct)
    {
        var settings = await repo.ListAsync(ct);

        return settings
            .Where(s => _publicKeys.Contains(s.Key) && !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new PublicSettingDto(s.Key, s.Value))
            .ToList();
    }
}
