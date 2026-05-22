namespace Innovayse.Application.Billing.Commands.DeleteQuote;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Permanently deletes a quote.</summary>
public sealed class DeleteQuoteHandler(IQuoteRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteQuoteCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the deletion is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not found.</exception>
    public async Task HandleAsync(DeleteQuoteCommand cmd, CancellationToken ct)
    {
        var quote = await repo.FindByIdAsync(cmd.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {cmd.QuoteId} not found.");

        repo.Remove(quote);
        await uow.SaveChangesAsync(ct);
    }
}
