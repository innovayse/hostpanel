using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// In-memory plugin registry for the use case tests: a catalogue the test fills with <see cref="Publish"/>, a
/// configurable failure, and a record of what was asked. The wire behaviour of the real client is tested on its own.
/// </summary>
public sealed class FakePluginRegistryClient : IPluginRegistryClient
{
    /// <summary>The listed plugins.</summary>
    private readonly List<RegistryPluginDto> _entries = [];

    /// <summary>The published releases by plugin id and version.</summary>
    private readonly Dictionary<(string Id, string Version), RegistryPluginRelease> _releases = [];

    /// <summary>The package bytes by plugin id and version.</summary>
    private readonly Dictionary<(string Id, string Version), byte[]> _packages = [];

    /// <summary>The error code every call answers with when set; <see langword="null"/> for a working registry.</summary>
    public string? FailureCode { get; set; }

    /// <summary>The last search the registry was asked for.</summary>
    public RegistryPluginSearch? LastSearch { get; private set; }

    /// <summary>The ids of the last newest-version lookup.</summary>
    public IReadOnlyCollection<string>? LastLatestIds { get; private set; }

    /// <summary>The licence key of the last release lookup, download, plugin page or version list call; <see langword="null"/> when none was given.</summary>
    public string? LastLicenceKey { get; private set; }

    /// <summary>The licence key each plugin page call carried, by plugin id; <see langword="null"/> when none was given.</summary>
    public Dictionary<string, string?> PluginPageKeys { get; } = [];

    /// <summary>The plugin pages by plugin id; an id that is not here reads as not listed.</summary>
    public Dictionary<string, RegistryPluginDetails> Details { get; } = [];

    /// <summary>The version lists by plugin id; an id that is not here has no versions.</summary>
    public Dictionary<string, IReadOnlyList<RegistryPluginVersion>> Versions { get; } = [];

    /// <summary>How many packages were downloaded.</summary>
    public int DownloadCount { get; private set; }

    /// <summary>Versions the registry shows only to a caller that sends a licence key, by plugin id; they are added to <see cref="Versions"/> for that caller.</summary>
    public Dictionary<string, IReadOnlyList<RegistryPluginVersion>> LicensedVersions { get; } = [];

    /// <summary>A failure code the version list answers with, while the rest of the registry keeps answering; <see langword="null"/> for none.</summary>
    public string? VersionsFailureCode { get; set; }

    /// <summary>How many times the version list was asked for.</summary>
    public int ListVersionsCount { get; private set; }

    /// <summary>Lists a plugin version in the registry and stores its package.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="version">The version; the newest published one is listed as the latest.</param>
    /// <param name="package">The package bytes.</param>
    /// <param name="verified">Whether the registry vouches for the publisher.</param>
    /// <param name="publishedSha256">The checksum the registry publishes; the real one when <see langword="null"/>.</param>
    /// <param name="category">The registry category.</param>
    /// <param name="status">Where the version stands in the registry; it is listed in <see cref="Versions"/> with it.</param>
    /// <param name="minHostVersion">The oldest host version the version runs on; <see langword="null"/> for no bound.</param>
    /// <param name="maxHostVersion">The newest host version the version runs on; <see langword="null"/> for no bound.</param>
    /// <param name="pricing">Whether the plugin is free, paid or private.</param>
    public void Publish(
        string pluginId,
        string version,
        byte[] package,
        bool verified = true,
        string? publishedSha256 = null,
        PluginRegistryCategory category = PluginRegistryCategory.Other,
        PluginRegistryVersionStatus status = PluginRegistryVersionStatus.Published,
        string? minHostVersion = null,
        string? maxHostVersion = null,
        PluginRegistryPricing pricing = PluginRegistryPricing.Free)
    {
        _entries.RemoveAll(e => e.Id == pluginId);
        _entries.Add(new RegistryPluginDto(
            pluginId, $"Plugin {pluginId}", "Test", "A test plugin.", null, category, version, "1.0", verified, pricing));
        var listed = Versions.GetValueOrDefault(pluginId)?.Where(v => v.Version != version).ToList() ?? [];
        listed.Add(new RegistryPluginVersion(
            version, "1.0", package.Length, DateTimeOffset.UtcNow, status, null, null, minHostVersion, maxHostVersion, null, [], null, []));
        Versions[pluginId] = listed;
        Details[pluginId] = new RegistryPluginDetails(_entries.Single(e => e.Id == pluginId), null, null, null, null, null, null, null);
        _releases[(pluginId, version)] = new RegistryPluginRelease(
            pluginId,
            version,
            new Uri($"https://registry.example.com/files/{pluginId}-{version}.zip"),
            publishedSha256 ?? Convert.ToHexStringLower(SHA256.HashData(package)),
            verified);
        _packages[(pluginId, version)] = package;
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginPageDto>> SearchAsync(RegistryPluginSearch search, CancellationToken ct)
    {
        LastSearch = search;
        if (FailureCode is not null)
        {
            return Task.FromResult(Result<RegistryPluginPageDto>.FailureCode(FailureCode));
        }

        var matches = _entries
            .Where(e => search.Category is null || e.Category == search.Category)
            .Where(e => search.Search is null || e.Name.Contains(search.Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
        var page = matches.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToList();
        return Task.FromResult(Result<RegistryPluginPageDto>.Success(new RegistryPluginPageDto(page, matches.Count, search.Page, search.PageSize)));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RegistryPluginDto>>> GetLatestAsync(IReadOnlyCollection<string> pluginIds, CancellationToken ct)
    {
        LastLatestIds = pluginIds;
        if (FailureCode is not null)
        {
            return Task.FromResult(Result<IReadOnlyList<RegistryPluginDto>>.FailureCode(FailureCode));
        }

        IReadOnlyList<RegistryPluginDto> known = _entries.Where(e => pluginIds.Contains(e.Id)).ToList();
        return Task.FromResult(Result<IReadOnlyList<RegistryPluginDto>>.Success(known));
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginRelease>> GetReleaseAsync(RegistryPluginVersionRef target, CancellationToken ct, string? licenceKey = null)
    {
        LastLicenceKey = licenceKey;
        var (pluginId, version) = (target.PluginId, target.Version);
        if (FailureCode is not null)
        {
            return Task.FromResult(Result<RegistryPluginRelease>.FailureCode(FailureCode));
        }

        return Task.FromResult(_releases.TryGetValue((pluginId, version), out var release)
            ? Result<RegistryPluginRelease>.Success(release)
            : Result<RegistryPluginRelease>.FailureCode("PLUGIN_REGISTRY_PLUGIN_NOT_FOUND"));
    }

    /// <inheritdoc/>
    public Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct, string? licenceKey = null)
    {
        LastLicenceKey = licenceKey;
        DownloadCount++;
        Stream stream = new MemoryStream(_packages[(release.PluginId, release.Version)]);
        return Task.FromResult(Result<Stream>.Success(stream));
    }

    /// <inheritdoc/>
    public Task<Result<RegistryPluginDetails?>> GetPluginAsync(string pluginId, string? licenceKey, CancellationToken ct)
    {
        LastLicenceKey = licenceKey;
        PluginPageKeys[pluginId] = licenceKey;
        return Task.FromResult(FailureCode is not null
            ? Result<RegistryPluginDetails?>.FailureCode(FailureCode)
            : Result<RegistryPluginDetails?>.Success(Details.GetValueOrDefault(pluginId)));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<RegistryPluginVersion>>> ListVersionsAsync(
        string pluginId, string? licenceKey, CancellationToken ct, PluginRegistryDistribution distribution = PluginRegistryDistribution.Download)
    {
        LastLicenceKey = licenceKey;
        ListVersionsCount++;
        var failure = FailureCode ?? VersionsFailureCode;
        return Task.FromResult(failure is not null
            ? Result<IReadOnlyList<RegistryPluginVersion>>.FailureCode(failure)
            : Result<IReadOnlyList<RegistryPluginVersion>>.Success(VisibleVersions(pluginId, licenceKey)));
    }

    /// <summary>The versions a caller sees: the public ones, and the licensed only ones when it sends a licence key.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="licenceKey">The licence key the caller sends, or <see langword="null"/>.</param>
    /// <returns>The versions.</returns>
    private IReadOnlyList<RegistryPluginVersion> VisibleVersions(string pluginId, string? licenceKey)
    {
        var shown = Versions.GetValueOrDefault(pluginId) ?? [];
        return string.IsNullOrEmpty(licenceKey) || !LicensedVersions.TryGetValue(pluginId, out var licensed) ? shown : [.. shown, .. licensed];
    }
}
