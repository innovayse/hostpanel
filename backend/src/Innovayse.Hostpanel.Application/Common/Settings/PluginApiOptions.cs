using Innovayse.Hostpanel.Domain.Plugins;

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

    /// <summary>
    /// How many characters one plugin store scope holds by default, keys and values together (4 Mi characters, about
    /// 8 MiB as stored text): room for 64 full-size values, or the default number of keys at about 4 KiB each.
    /// </summary>
    public const long DefaultMaxStoreCharsPerScope = 4_194_304;

    /// <summary>
    /// The smallest <see cref="MaxStoreCharsPerScope"/> a deployment may configure, in characters: one key of the longest
    /// length with one value of the longest length, so a scope can always hold at least one full write.
    /// </summary>
    public const long MaxStoreCharsPerScopeFloor = (long)PluginDataEntry.MaxKeyLength + PluginDataEntry.MaxValueLength;

    /// <summary>
    /// The largest <see cref="MaxStoreCharsPerScope"/> a deployment may configure, in characters (1 Gi): one scope is one
    /// plugin's data about one client, and anything larger belongs in the plugin's own storage.
    /// </summary>
    public const long MaxStoreCharsPerScopeCeiling = 1_073_741_824;

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

    /// <summary>The seconds a call to a plugin route may take by default before the host cancels it (30).</summary>
    public const int DefaultCallTimeoutSeconds = 30;

    /// <summary>The seconds the ceiling of a call's time limit is set to by default (10 minutes).</summary>
    public const int DefaultMaxCallTimeoutSeconds = 600;

    /// <summary>The largest <see cref="MaxCallTimeoutSeconds"/> a deployment may configure, in seconds (1 hour).</summary>
    public const int MaxCallTimeoutSecondsLimit = 3600;

    /// <summary>The seconds the host waits, after the call limit, for a plugin that ignores its token before it abandons the call (5).</summary>
    public const int DefaultAbandonWaitSeconds = 5;

    /// <summary>The largest <see cref="AbandonWaitSeconds"/> a deployment may configure, in seconds.</summary>
    public const int AbandonWaitSecondsCeiling = 60;

    /// <summary>How many abandoned calls of one plugin may still be running by default before new calls to it are refused (8).</summary>
    public const int DefaultMaxAbandonedCallsPerPlugin = 8;

    /// <summary>The largest <see cref="MaxAbandonedCallsPerPlugin"/> a deployment may configure.</summary>
    public const int MaxAbandonedCallsPerPluginCeiling = 1000;

    /// <summary>The seconds a plugin's status check (<c>IPluginStatus</c>) may take by default before the host stops waiting (5).</summary>
    public const int DefaultStatusTimeoutSeconds = 5;

    /// <summary>The largest <see cref="StatusTimeoutSeconds"/> a deployment may configure, in seconds.</summary>
    public const int StatusTimeoutSecondsCeiling = 60;

    /// <summary>The seconds a plugin's status check stays cached by default (300, as long as the hosting panel's server info).</summary>
    public const int DefaultStatusCacheSeconds = 300;

    /// <summary>The largest <see cref="StatusCacheSeconds"/> a deployment may configure, in seconds (1 day).</summary>
    public const int StatusCacheSecondsCeiling = 86_400;

    /// <summary>
    /// The most bytes of request body a plugin route accepts. A call that declares more, or sends more, is refused
    /// with <c>PLUGIN_REQUEST_TOO_LARGE</c> (413) before the plugin is called. The plugin sees the whole body or none of it.
    /// </summary>
    public int MaxBodyBytes { get; set; } = DefaultMaxBodyBytes;

    /// <summary>
    /// The seconds the host waits for a plugin route (1 to <see cref="MaxCallTimeoutSeconds"/>); every route gets it today. Past it the call is
    /// cancelled through the token the plugin receives and the caller gets <c>PLUGIN_TIMEOUT</c> (504). On a streaming route
    /// it bounds the wait for the reply, for the first event and the silence between events, never the length of a stream
    /// that keeps sending.
    /// </summary>
    public int CallTimeoutSeconds { get; set; } = DefaultCallTimeoutSeconds;

    /// <summary>
    /// The longest limit any route may have (1 to <see cref="MaxCallTimeoutSecondsLimit"/> seconds), and never below
    /// <see cref="CallTimeoutSeconds"/>. Today every route gets <see cref="CallTimeoutSeconds"/>; once the plugin SDK lets a
    /// route ask for a longer limit, the request is honoured up to this ceiling (<see cref="CallTimeoutFor"/>).
    /// </summary>
    public int MaxCallTimeoutSeconds { get; set; } = DefaultMaxCallTimeoutSeconds;

    /// <summary>
    /// The seconds the host waits, after the call limit has passed, for a plugin that ignores its token to end by itself
    /// (1 to <see cref="AbandonWaitSecondsCeiling"/>). After it the call is abandoned: the caller gets <c>PLUGIN_TIMEOUT</c>
    /// and the plugin's work, which the host cannot stop, runs on counted against <see cref="MaxAbandonedCallsPerPlugin"/>.
    /// </summary>
    public int AbandonWaitSeconds { get; set; } = DefaultAbandonWaitSeconds;

    /// <summary>
    /// The most abandoned calls of one plugin that may still be running (1 to <see cref="MaxAbandonedCallsPerPluginCeiling"/>).
    /// While a plugin is at the limit, new calls to it are refused at once with <c>PLUGIN_TIMEOUT</c> instead of piling
    /// more work that ignores its token on the host; a call that ends frees its place.
    /// </summary>
    public int MaxAbandonedCallsPerPlugin { get; set; } = DefaultMaxAbandonedCallsPerPlugin;

    /// <summary>
    /// The seconds the host waits for a plugin's status check (1 to <see cref="StatusTimeoutSecondsCeiling"/>). The check
    /// runs while an admin page loads, so it is short; past it the plugin's token is cancelled, the host stops waiting
    /// and the plugin is shown as unavailable.
    /// </summary>
    public int StatusTimeoutSeconds { get; set; } = DefaultStatusTimeoutSeconds;

    /// <summary>
    /// The seconds a plugin's status check is reused for the Integrations page (1 to <see cref="StatusCacheSecondsCeiling"/>).
    /// A Test Connection and a saved integration renew it at once.
    /// </summary>
    public int StatusCacheSeconds { get; set; } = DefaultStatusCacheSeconds;

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
    /// that would add a new key past it is refused with <c>PLUGIN_STORE_QUOTA_EXCEEDED</c> (402); replacing an existing
    /// key never adds one.
    /// </summary>
    public int MaxStoreKeysPerScope { get; set; } = DefaultMaxStoreKeysPerScope;

    /// <summary>
    /// The most characters a plugin keeps in one store scope, every key and every value counted (UTF-16 characters, as
    /// stored; <see cref="MaxStoreCharsPerScopeFloor"/> to <see cref="MaxStoreCharsPerScopeCeiling"/>). A write that
    /// would grow the scope past it is refused with <c>PLUGIN_STORE_QUOTA_EXCEEDED</c> (402); a write that shrinks or
    /// keeps the scope's size, and a removal, never is.
    /// </summary>
    public long MaxStoreCharsPerScope { get; set; } = DefaultMaxStoreCharsPerScope;

    /// <summary>Gets <see cref="MaxStoreKeysPerScope"/> and <see cref="MaxStoreCharsPerScope"/> as the one quota every store write is checked against.</summary>
    public PluginStoreQuota StoreQuota => new(MaxStoreKeysPerScope, MaxStoreCharsPerScope);

    /// <summary>
    /// Gets the limit the web server is given for a plugin request: the cap, one read chunk, and 1/64 of the cap for the
    /// framing of a chunked body. The server counts the chunk headers it strips (about 9 bytes per chunk), so a body of
    /// exactly the cap sent in 64 KiB chunks reaches the server's count above the cap by about 0.014%; the allowance
    /// covers chunks down to 512 bytes. Below that a body near the cap meets the server's bare 413 first, which is still a
    /// refusal and still bounded. The reader stops one chunk past the cap, so it answers its own 413 before this limit.
    /// </summary>
    public long ServerBodyLimit => (long)MaxBodyBytes + ReadChunkBytes + (MaxBodyBytes / 64L);

    /// <summary>
    /// Gets the limit of a call to a route: what the route asked for, or the default, and never more than the ceiling. The SDK's
    /// route record has no such field yet, so callers pass <see langword="null"/> and every route gets
    /// <see cref="CallTimeoutSeconds"/>; a per route limit waits for the SDK field.
    /// </summary>
    /// <param name="requestedSeconds">The limit the route's manifest asks for, or <see langword="null"/> (or zero or less) for none.</param>
    /// <returns>The time the host waits for the plugin.</returns>
    public TimeSpan CallTimeoutFor(int? requestedSeconds)
    {
        var seconds = requestedSeconds is > 0 ? requestedSeconds.Value : CallTimeoutSeconds;
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxCallTimeoutSeconds));
    }

    /// <summary>Gets <see cref="StatusTimeoutSeconds"/> as a span.</summary>
    public TimeSpan StatusTimeout => TimeSpan.FromSeconds(StatusTimeoutSeconds);

    /// <summary>Gets <see cref="StatusCacheSeconds"/> as a span.</summary>
    public TimeSpan StatusCacheLifetime => TimeSpan.FromSeconds(StatusCacheSeconds);

    /// <summary>Gets <see cref="AbandonWaitSeconds"/> as a span.</summary>
    public TimeSpan AbandonWait => TimeSpan.FromSeconds(AbandonWaitSeconds);

    /// <summary>Gets <see cref="StreamHeartbeatSeconds"/> as a span.</summary>
    public TimeSpan StreamHeartbeat => TimeSpan.FromSeconds(StreamHeartbeatSeconds);

    /// <summary>
    /// Whether the values are usable: a body cap of at least one byte and no more than <see cref="MaxBodyBytesCeiling"/>, and a
    /// call limit of one second to the ceiling, which is one second to <see cref="MaxCallTimeoutSecondsLimit"/>, and an abandon wait of
    /// one second to <see cref="AbandonWaitSecondsCeiling"/>, and 1 to <see cref="MaxAbandonedCallsPerPluginCeiling"/> abandoned calls per plugin, a status limit of one second to
    /// <see cref="StatusTimeoutSecondsCeiling"/> and a status cache of one second to <see cref="StatusCacheSecondsCeiling"/>, and a
    /// heartbeat of one second to <see cref="StreamHeartbeatSecondsCeiling"/>, and an event cap of one byte to <see cref="StreamEventMaxBytesCeiling"/>, and 1 to
    /// <see cref="MaxStoreKeysPerScopeCeiling"/> store keys per scope, and <see cref="MaxStoreCharsPerScopeFloor"/> to
    /// <see cref="MaxStoreCharsPerScopeCeiling"/> store characters per scope.
    /// </summary>
    public bool IsValid => MaxBodyBytes is >= 1 and <= MaxBodyBytesCeiling
        && MaxCallTimeoutSeconds is >= 1 and <= MaxCallTimeoutSecondsLimit
        && CallTimeoutSeconds >= 1 && CallTimeoutSeconds <= MaxCallTimeoutSeconds
        && AbandonWaitSeconds is >= 1 and <= AbandonWaitSecondsCeiling
        && MaxAbandonedCallsPerPlugin is >= 1 and <= MaxAbandonedCallsPerPluginCeiling
        && StatusTimeoutSeconds is >= 1 and <= StatusTimeoutSecondsCeiling
        && StatusCacheSeconds is >= 1 and <= StatusCacheSecondsCeiling
        && StreamHeartbeatSeconds is >= 1 and <= StreamHeartbeatSecondsCeiling
        && StreamEventMaxBytes is >= 1 and <= StreamEventMaxBytesCeiling
        && MaxStoreKeysPerScope is >= 1 and <= MaxStoreKeysPerScopeCeiling
        && MaxStoreCharsPerScope is >= MaxStoreCharsPerScopeFloor and <= MaxStoreCharsPerScopeCeiling;
}
