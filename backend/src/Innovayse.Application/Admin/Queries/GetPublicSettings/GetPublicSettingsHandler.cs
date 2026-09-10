namespace Innovayse.Application.Admin.Queries.GetPublicSettings;

using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="GetPublicSettingsQuery"/> by returning only the settings the
/// public storefront needs.
/// <para>
/// The keys come from <see cref="PortalSettingKeys.Public"/> — an allow-list rather
/// than a prefix match on purpose. A prefix would silently start exposing anything a
/// future contributor happens to name <c>portal.*</c>, and this response is served
/// without authentication from a table that also holds integration credentials.
/// </para>
/// </summary>
/// <param name="repo">Setting repository.</param>
public sealed class GetPublicSettingsHandler(ISettingRepository repo)
{
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
            .Where(s => PortalSettingKeys.Public.Contains(s.Key) && !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new PublicSettingDto(s.Key, s.Value))
            .ToList();
    }
}
