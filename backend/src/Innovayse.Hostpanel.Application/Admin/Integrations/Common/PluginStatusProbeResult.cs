using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>
/// What one status check of a plugin found, in no language: the host's sentence is chosen when it is read, so one
/// cached result serves every admin language.
/// </summary>
/// <param name="Outcome">How the check ended.</param>
/// <param name="Health">The health the plugin reported; <see langword="null"/> unless <paramref name="Outcome"/> is <see cref="PluginStatusProbeOutcome.Reported"/>.</param>
/// <param name="Detail">
/// The plugin's own short sentence for the administrator, as it wrote it (a key of its manifest locales or plain
/// text), trimmed and cut to <see cref="PluginStatusProbeResult.MaxDetailLength"/> characters; <see langword="null"/> for none.
/// Never host or exception text.
/// </param>
public sealed record PluginStatusProbeResult(PluginStatusProbeOutcome Outcome, PluginHealth? Health = null, string? Detail = null)
{
    /// <summary>The longest plugin detail kept, in characters, so a plugin cannot fill the page or the cache.</summary>
    public const int MaxDetailLength = 500;
}
