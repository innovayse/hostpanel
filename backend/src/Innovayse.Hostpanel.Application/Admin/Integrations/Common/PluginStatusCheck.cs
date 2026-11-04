namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>One status check of a plugin as <see cref="PluginConnectionReader"/> caches it.</summary>
/// <param name="Result">What the check found, in no language.</param>
/// <param name="CheckedAt">When the check ran, UTC.</param>
public sealed record PluginStatusCheck(PluginStatusProbeResult Result, DateTimeOffset CheckedAt);
