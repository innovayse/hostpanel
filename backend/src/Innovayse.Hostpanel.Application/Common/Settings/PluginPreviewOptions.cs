namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>The plugin preview block's settings. Bound from the "PluginPreview" section.</summary>
public sealed class PluginPreviewOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PluginPreview";

    /// <summary>
    /// Exact <c>http</c> origins on a loopback host a preview may frame, such as <c>http://localhost:8088</c> for a local
    /// site host. Empty by default; a start outside the Development environment refuses any entry, so a production host can
    /// only ever frame <c>https</c>. A blank entry counts as unset, because the dev compose file passes an empty value when no
    /// origin is chosen.
    /// </summary>
    public IReadOnlyList<string> DevelopmentHttpOrigins { get; set; } = [];
}
