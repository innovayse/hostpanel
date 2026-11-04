using Innovayse.SDK.Plugins.Ui;

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

    /// <summary>Gets the <c>id</c> of every block (an object with a string <c>kind</c>) in the document.</summary>
    public HashSet<string> BlockIds { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the block ids every <c>followUp</c> asks to refresh, in document order.</summary>
    public List<string> FollowUpRefresh { get; } = [];

    /// <summary>
    /// Gets or sets the first broken view state rule (<c>visibleWhen</c>, <c>followUp</c>, <c>resetOnSuccess</c>), its
    /// detail without the document's place, or <see langword="null"/> while none is broken.
    /// </summary>
    public PluginManifestViolation? StateViolation { get; set; }

    /// <summary>Gets or sets what is malformed, or <see langword="null"/> while nothing is.</summary>
    public string? Malformed { get; set; }
}
