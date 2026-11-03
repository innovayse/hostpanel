namespace Innovayse.Hostpanel.Application.Plugins.Common.Enums;

/// <summary>The events the host itself writes into a plugin's stream, besides the plugin's own; the name on the wire is the lower-case member name.</summary>
public enum PluginStreamEvent
{
    /// <summary>The plugin failed after the stream started: the data carries <c>code</c>, the localized <c>message</c> and <c>partial</c>.</summary>
    Error = 1,

    /// <summary>The host ends the stream after an <see cref="Error"/>; a stream that ends cleanly sends nothing more.</summary>
    Done = 2,
}
