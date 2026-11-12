using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Domain.Fraud;

namespace Innovayse.Hostpanel.Application.Fraud.Extensions;

/// <summary>Maps a <see cref="BlockListEntry"/> to its DTO.</summary>
public static class BlockListEntryExtensions
{
    /// <summary>Maps a block list entry.</summary>
    /// <param name="entry">The entry to map.</param>
    /// <returns>The DTO.</returns>
    public static BlockListEntryDto ToDto(this BlockListEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new BlockListEntryDto(entry.Id, entry.Type, entry.Value, entry.Note, entry.CreatedAt);
    }
}
