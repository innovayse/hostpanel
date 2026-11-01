using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One manifest rule a plugin breaks, as <see cref="PluginManifestValidator"/> reports it.</summary>
/// <param name="Rule">The rule that is broken; it picks the error code of an install and names the reason of a skipped load.</param>
/// <param name="Detail">Where and how the rule is broken, for the log: the member of the manifest and its value.</param>
internal sealed record PluginManifestViolation(PluginManifestRule Rule, string Detail);
