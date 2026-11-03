using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// The running totals of one plugin store scope (one plugin and one opaque client reference, or the plugin-wide scope):
/// how many keys it holds and how many characters its keys and values take. Every write of the scope goes through
/// <see cref="Admit"/> or <see cref="Release"/>, which is where the <see cref="PluginStoreQuota"/> is enforced.
/// </summary>
/// <remarks>
/// The quota is exact because every write of a scope also modifies this row: both methods move <see cref="Revision"/>,
/// so even a write that changes neither total (a value replaced by one of the same length) updates the row, and the
/// row's concurrency token (Postgres <c>xmin</c>) makes two writes of one scope that ran at the same time conflict on
/// save. Exactly one of them commits; the other is rolled back with its entry change and retried from a fresh read of
/// the totals. Two new keys can therefore never both pass a check against the same stale count, and the counters stay
/// equal to the rows they describe.
/// </remarks>
public sealed class PluginDataScope : BaseEntity, IAggregateRoot
{
    /// <summary>Gets the plugin whose scope this is.</summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the plugin's opaque reference of the client, or <see langword="null"/> for the plugin-wide scope; at most
    /// <see cref="PluginDataEntry.ClientRefLength"/> characters.
    /// </summary>
    public string? ClientRef { get; private set; }

    /// <summary>Gets how many keys the scope holds (count, never negative).</summary>
    public int KeyCount { get; private set; }

    /// <summary>
    /// Gets how many characters the scope's keys and values take together (<see cref="PluginDataEntry.CharsOf"/> summed,
    /// never negative).
    /// </summary>
    public long StoredChars { get; private set; }

    /// <summary>
    /// Gets how many writes the scope has taken. Moved by every <see cref="Admit"/> and <see cref="Release"/>, so every
    /// write modifies this row and its concurrency token serializes the writes of one scope.
    /// </summary>
    public long Revision { get; private set; }

    /// <summary>
    /// Whether a write that adds <paramref name="addedKeys"/> keys and <paramref name="addedChars"/> characters fits
    /// <paramref name="quota"/>. A measure the write does not grow is never a reason to refuse it, so a write that
    /// shrinks or keeps the scope's size is admitted even in a scope that is already over a lowered quota.
    /// </summary>
    /// <param name="addedKeys">Keys the write adds: 1 for a new key, 0 for a replaced value.</param>
    /// <param name="addedChars">Characters the write adds; negative when the new value is shorter than the old one.</param>
    /// <param name="quota">What the scope may hold.</param>
    /// <returns><see langword="true"/> when neither total the write grows would pass its cap.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="addedKeys"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="quota"/> is null.</exception>
    public bool CanAdmit(int addedKeys, long addedChars, PluginStoreQuota quota)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(addedKeys);
        ArgumentNullException.ThrowIfNull(quota);

        var keysFit = addedKeys == 0 || (long)KeyCount + addedKeys <= quota.MaxKeys;
        var charsFit = addedChars <= 0 || StoredChars + addedChars <= quota.MaxChars;
        return keysFit && charsFit;
    }

    /// <summary>EF Core materialisation only.</summary>
    private PluginDataScope()
    {
    }

    /// <summary>Opens an empty scope. The only way a scope row comes into existence; it is created with its first write.</summary>
    /// <param name="pluginId">The plugin whose scope this is.</param>
    /// <param name="clientRef">The plugin's opaque reference of the client, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <returns>The new, unsaved scope, holding nothing.</returns>
    public static PluginDataScope Open(string pluginId, string? clientRef)
    {
        return new PluginDataScope { PluginId = pluginId, ClientRef = clientRef };
    }

    /// <summary>
    /// Takes one write into the totals: a new key (<paramref name="addedKeys"/> 1) or a replaced value (0), with the
    /// characters it adds or, when negative, frees. Moves <see cref="Revision"/> whatever the change.
    /// </summary>
    /// <param name="addedKeys">Keys the write adds: 1 for a new key, 0 for a replaced value.</param>
    /// <param name="addedChars">Characters the write adds; negative when the new value is shorter than the old one.</param>
    /// <param name="quota">What the scope may hold.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <see cref="PluginStoreErrors.QuotaExceeded"/> when the write would take a total it grows past
    /// <paramref name="quota"/>; nothing changes then.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="addedKeys"/> is negative, or when the change would take a total below zero.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="quota"/> is null.</exception>
    public void Admit(int addedKeys, long addedChars, PluginStoreQuota quota)
    {
        if (!CanAdmit(addedKeys, addedChars, quota))
        {
            throw new DomainRuleException(
                PluginStoreErrors.QuotaExceeded, "A plugin store scope holds no more keys or characters than the host allows.");
        }

        var chars = StoredChars + addedChars;
        if (chars < 0)
        {
            // A value cannot free more characters than the scope holds; reaching this means the totals no longer
            // describe the rows, and writing a negative total would hide that.
            throw new ArgumentOutOfRangeException(nameof(addedChars), "A write cannot free more characters than the scope holds.");
        }

        KeyCount += addedKeys;
        StoredChars = chars;
        Revision++;
    }

    /// <summary>Takes one removed key out of the totals. Moves <see cref="Revision"/>; a removal is never refused by the quota.</summary>
    /// <param name="removedChars">The characters the removed key and its value took (<see cref="PluginDataEntry.StoredChars"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="removedChars"/> is negative, or when the scope holds no key or fewer characters than
    /// are being removed.
    /// </exception>
    public void Release(long removedChars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(removedChars);
        if (KeyCount == 0 || removedChars > StoredChars)
        {
            // The totals never go negative: a removal the totals do not account for means they no longer describe the
            // rows, and clamping would hide that.
            throw new ArgumentOutOfRangeException(nameof(removedChars), "The scope does not hold what is being removed.");
        }

        KeyCount--;
        StoredChars -= removedChars;
        Revision++;
    }
}
