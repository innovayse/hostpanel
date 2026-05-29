namespace Innovayse.Application.Billing.Commands.UpdateQuote;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Updates an existing quote's details and line items (add, update, delete).
/// </summary>
public sealed class UpdateQuoteHandler(IQuoteRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateQuoteCommand"/>.
    /// </summary>
    /// <param name="cmd">The update quote command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> that completes when the update is persisted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not found.</exception>
    public async Task HandleAsync(UpdateQuoteCommand cmd, CancellationToken ct)
    {
        var quote = await repo.FindByIdAsync(cmd.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {cmd.QuoteId} not found.");

        quote.UpdateDetails(cmd.Subject, cmd.Status, cmd.ExpiryDate, cmd.Notes);

        foreach (var entry in cmd.Items)
        {
            if (entry.IsDeleted && entry.Id.HasValue)
            {
                quote.RemoveItem(entry.Id.Value);
            }
            else if (entry.Id.HasValue)
            {
                quote.UpdateItem(entry.Id.Value, entry.Description, entry.UnitPrice, entry.Quantity);
            }
            else
            {
                quote.AddItem(entry.Description, entry.UnitPrice, entry.Quantity);
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
