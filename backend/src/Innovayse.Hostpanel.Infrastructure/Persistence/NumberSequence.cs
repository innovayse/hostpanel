using Innovayse.Hostpanel.Application.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL implementation of <see cref="INumberSequence"/>: one database sequence per
/// <see cref="NumberKind"/>, advanced with <c>nextval</c>, which is atomic across connections.
/// </summary>
public sealed class NumberSequence : INumberSequence
{
    /// <summary>Name of the database sequence behind <see cref="NumberKind.Order"/>.</summary>
    public const string OrderSequenceName = "OrderNumberSequence";

    /// <summary>Name of the database sequence behind <see cref="NumberKind.CreditNote"/>.</summary>
    public const string CreditNoteSequenceName = "CreditNoteNumberSequence";

    /// <summary>The application DbContext.</summary>
    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="NumberSequence"/> class.</summary>
    /// <param name="db">The application DbContext.</param>
    public NumberSequence(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<long> NextAsync(NumberKind kind, CancellationToken ct)
    {
        // nextval takes the sequence as a regclass literal, which cannot be a parameter, so each
        // kind has its own fixed statement and the SQL text never contains a caller's value.
        var values = kind switch
        {
            NumberKind.Order => await _db.Database
                .SqlQuery<long>($"SELECT nextval('\"OrderNumberSequence\"') AS \"Value\"")
                .ToListAsync(ct),
            NumberKind.CreditNote => await _db.Database
                .SqlQuery<long>($"SELECT nextval('\"CreditNoteNumberSequence\"') AS \"Value\"")
                .ToListAsync(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No sequence exists for this kind of number."),
        };

        return values.Single();
    }
}
