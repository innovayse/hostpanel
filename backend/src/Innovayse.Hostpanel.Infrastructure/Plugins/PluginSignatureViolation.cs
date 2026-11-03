using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One signature check a plugin package fails, as <see cref="PluginSignatureVerifier"/> reports it.</summary>
/// <param name="Rule">The check that failed; it picks the error code of an install and names the reason of a skipped load.</param>
/// <param name="Detail">What was found, for the log.</param>
public sealed record PluginSignatureViolation(PluginSignatureRule Rule, string Detail);
