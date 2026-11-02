namespace Innovayse.Hostpanel.Application.Common.Settings;

/// <summary>
/// What the host allows a call to a plugin route to carry and to keep in its store. Bound from the "PluginApi" section; a deployment sets
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

    /// <summary>
    /// How many keys one plugin store scope (a plugin and one client, or a plugin's plugin-wide values) holds by default.
    /// Generous for settings, cursors and per-client state; a plugin that needs more belongs in its own storage.
    /// </summary>
    public const int DefaultMaxStoreKeysPerScope = 1000;

    /// <summary>The largest <see cref="MaxStoreKeysPerScope"/> a deployment may configure.</summary>
    public const int MaxStoreKeysPerScopeCeiling = 100_000;

    /// <summary>The size of one read of a request body, in bytes. The body reader reads in chunks of this size, and the web server's limit leaves one chunk above the cap.</summary>
    public const int ReadChunkBytes = 8192;

    /// <summary>The default silence, in seconds, after which a streamed reply sends a heartbeat comment.</summary>
    public const int DefaultStreamHeartbeatSeconds = 15;

    /// <summary>The longest <see cref="StreamHeartbeatSeconds"/> a deployment may configure, in seconds; a longer silence outlasts the read timeout of a proxy.</summary>
    public const int StreamHeartbeatSecondsCeiling = 300;

    /// <summary>The default cap on the data of one streamed event, in bytes (64 KiB).</summary>
    public const int DefaultStreamEventMaxBytes = 65_536;

    /// <summary>The largest <see cref="StreamEventMaxBytes"/> a deployment may configure, in bytes (1 MiB).</summary>
    public const int StreamEventMaxBytesCeiling = 1_048_576;

    /// <summary>
    /// The most bytes of request body a plugin route accepts. A call that declares more, or sends more, is refused
    /// with <c>PLUGIN_REQUEST_TOO_LARGE</c> (413) before the plugin is called. The plugin sees the whole body or none of it.
    /// </summary>
    public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

    /// <summary>
    /// The seconds a streamed plugin reply may stay silent before the host writes a comment line, so a proxy and the
    /// client see traffic and an idle connection is not dropped (15 by default).
    /// </summary>
    public int StreamHeartbeatSeconds { get; set; } = DefaultStreamHeartbeatSeconds;

    /// <summary>
    /// The most bytes (UTF-8) the data of one event of a streamed plugin reply may carry (64 KiB by default). A plugin that
    /// emits a larger event ends the stream with the <c>error</c> event, so one event cannot hold a connection's memory.
    /// </summary>
    public int StreamEventMaxBytes { get; set; } = DefaultStreamEventMaxBytes;

    /// <summary>
    /// The most keys a plugin keeps in one store scope (count, 1 to <see cref="MaxStoreKeysPerScopeCeiling"/>). A write
    /// that would add a new key past it is refused with <c>PLUGIN_STORE_KEY_LIMIT_REACHED</c>, answered as
    /// <c>PLUGIN_FAILED</c>; replacing an existing key is always allowed. With the 64 Ki character value cap this bounds
    /// one scope at about 64 Mi characters by default.
    /// </summary>
    public int MaxStoreKeysPerScope { get; set; } = DefaultMaxStoreKeysPerScope;

    /// <summary>
    /// Gets the limit the web server is given for a plugin request: the cap, one read chunk, and 1/64 of the cap for the
    /// framing of a chunked body. The server counts the chunk headers it strips (about 9 bytes per chunk), so a body of
    /// exactly the cap sent in 64 KiB chunks reaches the server's count above the cap by about 0.014%; the allowance
    /// covers chunks down to 512 bytes. Below that a body near the cap meets the server's bare 413 first, which is still a
    /// refusal and still bounded. The reader stops one chunk past the cap, so it answers its own 413 before this limit.
    /// </summary>
    public long ServerBodyLimit => (long)MaxBodyBytes + ReadChunkBytes + (MaxBodyBytes / 64L);

    /// <summary>Gets <see cref="StreamHeartbeatSeconds"/> as a span.</summary>
    public TimeSpan StreamHeartbeat => TimeSpan.FromSeconds(StreamHeartbeatSeconds);

    /// <summary>
    /// Whether the values are usable: a body cap of at least one byte and no more than <see cref="MaxBodyBytesCeiling"/>, and a
    /// heartbeat of one second to <see cref="StreamHeartbeatSecondsCeiling"/>, and an event cap of one byte to <see cref="StreamEventMaxBytesCeiling"/>, and 1 to
    /// <see cref="MaxStoreKeysPerScopeCeiling"/> store keys per scope.
    /// </summary>
    public bool IsValid => MaxBodyBytes is >= 1 and <= MaxBodyBytesCeiling
        && StreamHeartbeatSeconds is >= 1 and <= StreamHeartbeatSecondsCeiling
        && StreamEventMaxBytes is >= 1 and <= StreamEventMaxBytesCeiling
        && MaxStoreKeysPerScope is >= 1 and <= MaxStoreKeysPerScopeCeiling;
}
