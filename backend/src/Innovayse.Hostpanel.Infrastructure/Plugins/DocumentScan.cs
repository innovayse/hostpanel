using Hostpanel.Sdk.Plugins.Ui;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What a walk through one view document found: the routes, kinds and labels it names and the first problem, if any.</summary>
internal sealed class DocumentScan
{
    /// <summary>Gets the routes the document names, in document order.</summary>
    public List<PluginViewRoute> Routes { get; } = [];

    /// <summary>Gets the block kinds the document uses, in document order.</summary>
    public List<string> Kinds { get; } = [];

    /// <summary>Gets the label strings the document holds, in document order.</summary>
    public List<string> Labels { get; } = [];

    /// <summary>Gets or sets what is malformed, or <see langword="null"/> while nothing is.</summary>
    public string? Malformed { get; set; }
}
