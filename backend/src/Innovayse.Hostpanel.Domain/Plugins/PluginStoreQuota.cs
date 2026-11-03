namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// How much one plugin store scope (one plugin and one client reference, or one plugin wide) may hold: a number of keys
/// and a total size. One rule, asked by <see cref="PluginDataScope"/> on every write, so the key count and the size can
/// never be checked in two places that disagree.
/// </summary>
/// <param name="MaxKeys">The most keys the scope may hold (count).</param>
/// <param name="MaxChars">The most characters the scope may hold, counting every key and every value (UTF-16 characters, as stored).</param>
public sealed record PluginStoreQuota(int MaxKeys, long MaxChars);
