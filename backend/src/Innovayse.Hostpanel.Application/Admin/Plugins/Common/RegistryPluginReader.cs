using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one reader of what this host is shown of a registry plugin, shared by the details page and the update offer: the
/// stored licence key, the plugin's page read with it, the verification of this installation before a host only plugin's signed
/// version list, and the version list.
/// </summary>
/// <remarks>
/// The reader is scoped, so it serves one request: it reads each licence key and each page once, verifies the installation
/// once, and answers every call after the first outage of the registry with that outage instead of asking again. Reading a
/// host only plugin can register and verify this installation with the registry, so a read can have that one write effect.
/// </remarks>
public sealed class RegistryPluginReader
{
    /// <summary>The plugin registry.</summary>
    private readonly IPluginRegistryClient _registry;

    /// <summary>Reads the stored licence keys.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Decrypts the stored licence keys.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Makes sure this installation is verified by the registry before the versions of a host only plugin are asked for.</summary>
    private readonly RegistryInstanceEnrollment _enrollment;

    /// <summary>The licence keys read in this request, by plugin id; a plugin without a key is stored as <see langword="null"/>.</summary>
    private readonly Dictionary<string, string?> _licences = new(StringComparer.Ordinal);

    /// <summary>The pages read in this request, by plugin id.</summary>
    private readonly Dictionary<string, Result<RegistryPluginDetails?>> _pages = new(StringComparer.Ordinal);

    /// <summary>The answer of the verification, once asked in this request.</summary>
    private Result? _verified;

    /// <summary>The code of the first outage of the registry in this request; <see langword="null"/> while it answered.</summary>
    private string? _outage;

    /// <summary>Initializes the reader.</summary>
    /// <param name="registry">The plugin registry.</param>
    /// <param name="db">Reads the stored licence keys.</param>
    /// <param name="cipher">Decrypts the stored licence keys.</param>
    /// <param name="enrollment">Makes sure this installation is verified by the registry.</param>
    public RegistryPluginReader(
        IPluginRegistryClient registry, IHostpanelDbContext db, ISecretSettingCipher cipher, RegistryInstanceEnrollment enrollment)
    {
        _registry = registry;
        _db = db;
        _cipher = cipher;
        _enrollment = enrollment;
    }

    /// <summary>Reads the licence key stored for a plugin, once per request.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The key, or <see langword="null"/> when none is stored.</returns>
    public async Task<string?> LicenceAsync(string pluginId, CancellationToken ct)
    {
        if (!_licences.TryGetValue(pluginId, out var licence))
        {
            licence = await _db.GetRegistryLicenceAsync(_cipher, pluginId, ct);
            _licences[pluginId] = licence;
        }

        return licence;
    }

    /// <summary>Reads the page of a plugin with its stored licence key, once per request.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page (<see langword="null"/> when the registry does not list the plugin), or the registry's failure.</returns>
    public async Task<Result<RegistryPluginDetails?>> PageAsync(string pluginId, CancellationToken ct)
    {
        if (_pages.TryGetValue(pluginId, out var cached))
        {
            return cached;
        }

        if (_outage is not null)
        {
            return Result<RegistryPluginDetails?>.FailureCode(_outage);
        }

        var page = await _registry.GetPluginAsync(pluginId, await LicenceAsync(pluginId, ct), ct);
        NoteOutage(page);
        _pages[pluginId] = page;
        return page;
    }

    /// <summary>Reads what this host is shown of a plugin: the page, then the version list the page's distribution calls for.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The view; <c>PLUGIN_REGISTRY_PLUGIN_NOT_FOUND</c> when the registry does not list the plugin; for a host only plugin
    /// <c>PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED</c> and the other <c>PLUGIN_REGISTRY_INSTANCE_*</c> failures of the registration or of a
    /// signed call; or another <c>PLUGIN_REGISTRY_*</c> failure.
    /// </returns>
    public async Task<Result<RegistryPluginView>> ReadAsync(string pluginId, CancellationToken ct)
    {
        var page = await PageAsync(pluginId, ct);
        if (page.IsFailure)
        {
            return Result<RegistryPluginView>.FailureCode(page.ErrorCode!);
        }

        var details = page.Value;
        if (details is null || !string.Equals(details.Summary.Id, pluginId, StringComparison.Ordinal))
        {
            return Result<RegistryPluginView>.FailureCode(HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        }

        // A host only plugin lists its versions only to a verified installation, with no licence key.
        var distribution = details.Summary.Distribution;
        if (distribution == PluginRegistryDistribution.HostOnly)
        {
            var verified = await VerifiedAsync(ct);
            if (verified.IsFailure)
            {
                return Result<RegistryPluginView>.FailureCode(verified.ErrorCode!);
            }
        }

        var licence = await LicenceAsync(pluginId, ct);
        var listed = _outage is null
            ? await _registry.ListVersionsAsync(pluginId, licence, ct, distribution)
            : Result<IReadOnlyList<RegistryPluginVersion>>.FailureCode(_outage);
        NoteOutage(listed);
        return listed.IsFailure
            ? Result<RegistryPluginView>.FailureCode(listed.ErrorCode!)
            : Result<RegistryPluginView>.Success(new RegistryPluginView(details, licence, listed.Value!));
    }

    /// <summary>Verifies this installation with the registry, asking once per request.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The answer of the verification.</returns>
    private async Task<Result> VerifiedAsync(CancellationToken ct)
    {
        if (_verified is null)
        {
            _verified = _outage is null ? await _enrollment.EnsureVerifiedAsync(force: false, ct) : Result.FailureCode(_outage);
            NoteOutage(_verified);
        }

        return _verified;
    }

    /// <summary>
    /// Remembers the first time the registry could not be used (switched off, unreachable, too slow or answering with something that
    /// cannot be read), so the rest of the request does not wait on it again. A refusal about one plugin (not found, a licence
    /// problem, a host that is not verified) says nothing about the registry and is not remembered.
    /// </summary>
    /// <param name="outcome">The answer of a registry call.</param>
    private void NoteOutage(Result outcome)
    {
        if (outcome.IsFailure
            && outcome.ErrorCode is HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED
                or HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE
                or HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT
                or HostpanelErrorCodes.PLUGIN_REGISTRY_RESPONSE_INVALID)
        {
            _outage ??= outcome.ErrorCode;
        }
    }
}
