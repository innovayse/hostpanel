namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>What a plugin archive says about itself before anything is unpacked, read the way the package store reads it.</summary>
/// <param name="PluginId">The <c>id</c> in the archive's <c>plugin.json</c>; <see langword="null"/> when there is none or it is unreadable.</param>
/// <param name="Version">The <c>version</c> in the archive's <c>plugin.json</c>; <see langword="null"/> when unreadable.</param>
/// <param name="KeyId">The key id in the archive's <c>plugin.sig</c>; <see langword="null"/> when unsigned or unreadable.</param>
public sealed record PluginPackageHeader(string? PluginId, string? Version, string? KeyId);
