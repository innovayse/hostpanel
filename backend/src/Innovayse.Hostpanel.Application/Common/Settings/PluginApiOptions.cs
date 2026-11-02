namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>
/// What the host allows a call to a plugin route to carry. Bound from the "PluginApi" section; a deployment sets
/// it only to change the defaults.
/// </summary>
public sealed class PluginApiOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "PluginApi";

    /// <summary>The largest request body the host reads for a plugin, in bytes (1 MiB by default).</summary>
    public const int DefaultMaxBodyBytes = 1_048_576;

    /// <summary>The largest <see cref="MaxBodyBytes"/> a deployment may configure, in bytes (64 MiB).</summary>
    public const int MaxBodyBytesCeiling = 67_108_864;

    /// <summary>The size of one read of a request body, in bytes. The body reader reads in chunks of this size, and the web server's limit leaves one chunk above the cap.</summary>
    public const int ReadChunkBytes = 8192;

    /// <summary>
    /// The most bytes of request body a plugin route accepts. A call that declares more, or sends more, is refused
    /// with <c>PLUGIN_REQUEST_TOO_LARGE</c> (413) before the plugin is called. The plugin sees the whole body or none of it.
    /// </summary>
    public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

    /// <summary>
    /// Gets the limit the web server is given for a plugin request: the cap, one read chunk, and 1/64 of the cap for the
    /// framing of a chunked body. The server counts the chunk headers it strips (about 9 bytes per chunk), so a body of
    /// exactly the cap sent in 64 KiB chunks reaches the server's count above the cap by about 0.014%; the allowance
    /// covers chunks down to 512 bytes. Below that a body near the cap meets the server's bare 413 first, which is still a
    /// refusal and still bounded. The reader stops one chunk past the cap, so it answers its own 413 before this limit.
    /// </summary>
    public long ServerBodyLimit => (long)MaxBodyBytes + ReadChunkBytes + (MaxBodyBytes / 64L);

    /// <summary>Whether the values can bound a body: at least one byte and no more than <see cref="MaxBodyBytesCeiling"/>.</summary>
    public bool IsValid => MaxBodyBytes is >= 1 and <= MaxBodyBytesCeiling;
}
