using Innovayse.Hostpanel.Application.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Hands out the next number of a kind, atomically, however many callers ask at once.
/// </summary>
/// <remarks>
/// A number derived from the highest stored value plus one is read and used in two steps, so two
/// concurrent callers read the same maximum and both claim the same number; the unique index then
/// refuses the second, which surfaced as a server error. A counter owned by the database hands each
/// caller a distinct value in a single step. Values are never handed out twice, but a value taken by
/// a caller whose work is later abandoned is not given back, so numbers may have gaps.
/// </remarks>
public interface INumberSequence
{
    /// <summary>Takes the next number of the given kind.</summary>
    /// <param name="kind">Which counter to advance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A number no earlier or concurrent caller of the same kind has received.</returns>
    public Task<long> NextAsync(NumberKind kind, CancellationToken ct);
}
