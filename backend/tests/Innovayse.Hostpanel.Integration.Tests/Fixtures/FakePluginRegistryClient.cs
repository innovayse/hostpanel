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

    /// <summary>How many packages were downloaded.</summary>
    public int DownloadCount { get; private set; }

    /// <summary>Lists a plugin version in the registry and stores its package.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="version">The version; the newest published one is listed as the latest.</param>
    /// <param name="package">The package bytes.</param>
    /// <param name="verified">Whether the registry vouches for the publisher.</param>
    /// <param name="publishedSha256">The checksum the registry publishes; the real one when <see langword="null"/>.</param>
    /// <param name="category">The registry category.</param>
    public void Publish(
        string pluginId,
        string version,
        byte[] package,
        bool verified = true,
        string? publishedSha256 = null,
        PluginRegistryCategory category = PluginRegistryCategory.Other)
    {
        _entries.RemoveAll(e => e.Id == pluginId);
        _entries.Add(new RegistryPluginDto(
            pluginId, $"Plugin {pluginId}", "Test", "A test plugin.", null, category, version, "1.0", verified, PluginRegistryPricing.Free));
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
    public Task<Result<RegistryPluginRelease>> GetReleaseAsync(string pluginId, string version, CancellationToken ct)
    {
        if (FailureCode is not null)
        {
            return Task.FromResult(Result<RegistryPluginRelease>.FailureCode(FailureCode));
        }

        return Task.FromResult(_releases.TryGetValue((pluginId, version), out var release)
            ? Result<RegistryPluginRelease>.Success(release)
            : Result<RegistryPluginRelease>.FailureCode("PLUGIN_REGISTRY_PLUGIN_NOT_FOUND"));
    }

    /// <inheritdoc/>
    public Task<Result<Stream>> DownloadAsync(RegistryPluginRelease release, CancellationToken ct)
    {
        DownloadCount++;
        Stream stream = new MemoryStream(_packages[(release.PluginId, release.Version)]);
        return Task.FromResult(Result<Stream>.Success(stream));
    }
}
