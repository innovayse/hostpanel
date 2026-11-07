using Hostpanel.Sdk.Plugins.Ui;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What a walk through one view document found: the routes, kinds and labels it names and the first problem, if any.</summary>
internal sealed class DocumentScan
{
    /// <summary>Gets or sets a value indicating whether the document holds an action with a body source.</summary>
    public bool HasActionBody { get; set; }

    /// <summary>Gets the routes the document names, in document order.</summary>
    public List<PluginViewRoute> Routes { get; } = [];

    /// <summary>Gets the block kinds the document uses, in document order.</summary>
    public List<string> Kinds { get; } = [];

    /// <summary>Gets the label strings the document holds, in document order.</summary>
    public List<string> Labels { get; } = [];

    /// <summary>Gets the <c>id</c> of every block (an object with a string <c>kind</c>) in the document.</summary>
    public HashSet<string> BlockIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the faults of rules that were added after plugins were installed (a block id given twice, a mistyped
    /// display member), each with its detail without the document's place. They are kept apart from
    /// <see cref="Malformed"/> and <see cref="StateViolation"/>, which refuse a plugin at load: an advisory refuses it
    /// only at install.
    /// </summary>
    public List<PluginManifestViolation> Advisories { get; } = [];

    /// <summary>Gets the block ids every <c>followUp</c> asks to refresh, in document order.</summary>
    public List<string> FollowUpRefresh { get; } = [];

    /// <summary>
    /// Gets or sets the first broken view state rule (<c>visibleWhen</c>, <c>followUp</c>, <c>resetOnSuccess</c>), its
    /// detail without the document's place, or <see langword="null"/> while none is broken.
    /// </summary>
    public PluginManifestViolation? StateViolation { get; set; }

    /// <summary>Gets or sets whether the document holds a <c>preview</c> block, which only a client page may show.</summary>
    public bool HasPreview { get; set; }

    /// <summary>Gets or sets whether a <c>preview</c> of the document offers element selection.</summary>
    public bool HasPreviewSelection { get; set; }

    /// <summary>Gets or sets whether a <c>chat</c> of the document takes the selection.</summary>
    public bool HasChatSelection { get; set; }

    /// <summary>Gets or sets how many <c>split</c> blocks enclose the element being walked; above zero, a split would nest.</summary>
    public int SplitDepth { get; set; }

    /// <summary>Gets or sets what is malformed, or <see langword="null"/> while nothing is.</summary>
    public string? Malformed { get; set; }
}
