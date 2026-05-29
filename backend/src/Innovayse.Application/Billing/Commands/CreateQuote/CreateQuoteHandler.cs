namespace Innovayse.Application.Billing.Commands.CreateQuote;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

<<<<<<< HEAD
/// <summary>Creates a new quote with the provided line items and persists it.</summary>
=======
/// <summary>
/// Creates a new quote with the provided line items and persists it.
/// </summary>
>>>>>>> origin/main
public sealed class CreateQuoteHandler(IQuoteRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateQuoteCommand"/>.
    /// </summary>
    /// <param name="cmd">The create quote command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created quote ID.</returns>
    public async Task<int> HandleAsync(CreateQuoteCommand cmd, CancellationToken ct)
    {
<<<<<<< HEAD
        var quote = Quote.Create(cmd.ClientId, cmd.Subject, cmd.ExpiryDate, cmd.Notes);

        foreach (var item in cmd.Items)
        {
            quote.AddItem(item.Description, item.UnitPrice, item.Quantity);
=======
        var quote = Quote.Create(
            cmd.ClientId, cmd.Subject, cmd.Stage, cmd.ValidUntil,
            cmd.ProposalText, cmd.CustomerNotes, cmd.AdminNotes);

        foreach (var item in cmd.Items)
        {
            quote.AddItem(item.Quantity, item.Description, item.UnitPrice, item.DiscountPercent, item.Taxed);
>>>>>>> origin/main
        }

        repo.Add(quote);
        await uow.SaveChangesAsync(ct);
        return quote.Id;
    }
}
