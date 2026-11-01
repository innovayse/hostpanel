using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The result of an install or removal through <see cref="Innovayse.Hostpanel.Application.Common.Interfaces.IPluginPackageStore"/>.</summary>
/// <param name="Outcome">How the operation ended.</param>
/// <param name="PluginId">The plugin id the operation concerned, when one was read; <see langword="null"/> otherwise.</param>
/// <param name="Sha256">The lower-case hex SHA-256 of the uploaded archive, for an install that read the whole upload; <see langword="null"/> otherwise.</param>
/// <param name="Rule">The manifest rule that was broken when <paramref name="Outcome"/> is <see cref="PluginPackageOutcome.ManifestRuleBroken"/>; <see langword="null"/> otherwise.</param>
public sealed record PluginPackageResult(
    PluginPackageOutcome Outcome, string? PluginId, string? Sha256, PluginManifestRule? Rule = null);
