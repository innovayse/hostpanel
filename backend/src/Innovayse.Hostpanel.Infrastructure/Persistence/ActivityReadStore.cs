using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// Stores the read marks of the notification feed with one statement that skips every mark already there.
/// </summary>
public sealed class ActivityReadStore : IActivityReadStore
{
    /// <summary>The database holding the <c>ActivityReads</c> rows.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ActivityReadStore"/> class.</summary>
    /// <param name="db">The database holding the <c>ActivityReads</c> rows.</param>
    public ActivityReadStore(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task MarkReadAsync(string userId, IReadOnlyCollection<string> eventKeys, DateTimeOffset readAt, CancellationToken ct)
    {
        if (eventKeys.Count == 0)
        {
            return;
        }

        // One statement that ignores a key already there, rather than a check followed by an insert: two calls
        // marking the same event at the same moment both pass a check, and the second insert then broke the key,
        // leaving a failed row tracked in the scope. The key is the pair, so the database decides, and nothing is
        // tracked, so nothing can fail a later save in the same scope.
        var keys = eventKeys.Distinct(StringComparer.Ordinal).ToArray();
        await _db.Database.ExecuteSqlAsync(
            $"""INSERT INTO "ActivityReads" ("UserId", "EventKey", "ReadAt") SELECT {userId}, k, {readAt} FROM unnest({keys}) AS k ON CONFLICT ("UserId", "EventKey") DO NOTHING""",
            ct);
    }
}
