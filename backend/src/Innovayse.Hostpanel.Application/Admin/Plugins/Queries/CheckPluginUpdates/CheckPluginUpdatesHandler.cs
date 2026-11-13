using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.CheckPluginUpdates;

/// <summary>
/// Handles <see cref="CheckPluginUpdatesQuery"/>: asks the registry for the newest version of every uploaded plugin
/// and keeps the ones that are newer than what is installed. Plugins shipped with the build are not asked about,
/// because an upload cannot replace them. A plugin with a licence key stored is asked about again with that key, because
/// the registry may show a paid or private plugin only to its licensee.
/// </summary>
public sealed class CheckPluginUpdatesHandler
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the plugins installed on disk.</summary>
    private readonly IPluginPackageStore _store;

    /// <summary>Reads the stored licence keys.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Decrypts the stored licence keys.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Sets the installed state and the update offer on a row.</summary>
    private readonly PluginUpdateOffer _offer;

    /// <summary>Initializes the handler.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="store">Reads the plugins installed on disk.</param>
    /// <param name="db">Reads the stored licence keys.</param>
    /// <param name="cipher">Decrypts the stored licence keys.</param>
    /// <param name="offer">Sets the installed state and the update offer on a row.</param>
    public CheckPluginUpdatesHandler(
        IPluginRegistryClient registry, IPluginPackageStore store, IHostpanelDbContext db, ISecretSettingCipher cipher, PluginUpdateOffer offer)
    {
        _registry = registry;
        _store = store;
        _db = db;
        _cipher = cipher;
        _offer = offer;
    }

    /// <summary>Returns the installed plugins that have a newer version in the registry.</summary>
    /// <param name="query">The query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updates (the same rows as the browser, installed and update flags set), in plugin id order (empty when nothing uploaded is installed, without asking the registry); or the
    /// registry's <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public async Task<Result<IReadOnlyList<RegistryPluginDto>>> HandleAsync(CheckPluginUpdatesQuery query, CancellationToken ct)
    {
        var uploaded = (await _store.ListAsync(ct))
            .Where(p => !p.IsBuiltIn)
            .GroupBy(p => p.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        if (uploaded.Count == 0)
        {
            return Result<IReadOnlyList<RegistryPluginDto>>.Success([]);
        }

        var latest = await _registry.GetLatestAsync(uploaded.Keys.ToList(), ct);
        if (latest.IsFailure)
        {
            return Result<IReadOnlyList<RegistryPluginDto>>.FailureCode(latest.ErrorCode!);
        }

        // A plugin with a stored key is read again with it; the licensee's view wins over the anonymous one, and a refusal
        // of one plugin's key does not fail the whole check (that plugin keeps the answer it had, or none).
        var entries = latest.Value!.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var id in uploaded.Keys)
        {
            var licence = await _db.GetRegistryLicenceAsync(_cipher, id, ct);
            if (licence is null)
            {
                continue;
            }

            var detail = await _registry.GetPluginAsync(id, licence, ct);
            if (!detail.IsFailure && detail.Value is not null && string.Equals(detail.Value.Summary.Id, id, StringComparison.Ordinal) && !string.IsNullOrEmpty(detail.Value.Summary.LatestVersion))
            {
                entries[detail.Value.Summary.Id] = detail.Value.Summary;
            }
        }

        var updates = new List<RegistryPluginDto>();
        foreach (var entry in entries.Values.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            // The registry may answer for an id that was not asked about; only installed plugins are reported.
            if (!uploaded.TryGetValue(entry.Id, out var package))
            {
                continue;
            }

            var row = await _offer.MarkAsync(entry, package, ct);
            if (row.UpdateAvailable)
            {
                updates.Add(row);
            }
        }

        return Result<IReadOnlyList<RegistryPluginDto>>.Success(updates);
    }
}
