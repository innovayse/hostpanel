namespace Innovayse.Application.Billing.Commands.DuplicateQuote;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Creates a draft copy of an existing quote.</summary>
public sealed class DuplicateQuoteHandler(IQuoteRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DuplicateQuoteCommand"/>.
    /// </summary>
    /// <param name="cmd">The duplicate command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created draft quote.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the source quote is not found.</exception>
    public async Task<int> HandleAsync(DuplicateQuoteCommand cmd, CancellationToken ct)
    {
        var source = await repo.FindByIdAsync(cmd.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {cmd.QuoteId} not found.");

        var copy = source.Duplicate();
        repo.Add(copy);
        await uow.SaveChangesAsync(ct);
        return copy.Id;
    }
}
