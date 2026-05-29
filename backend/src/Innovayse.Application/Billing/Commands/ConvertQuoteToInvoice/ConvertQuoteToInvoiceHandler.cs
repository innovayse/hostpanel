namespace Innovayse.Application.Billing.Commands.ConvertQuoteToInvoice;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Converts a quote into a draft invoice by copying its line items.
<<<<<<< HEAD
/// The quote status is set to <see cref="QuoteStatus.Accepted"/> after conversion.
=======
/// The quote stage is set to <see cref="QuoteStage.Accepted"/> after conversion.
>>>>>>> origin/main
/// </summary>
public sealed class ConvertQuoteToInvoiceHandler(
    IQuoteRepository quoteRepo,
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="ConvertQuoteToInvoiceCommand"/>.
    /// </summary>
    /// <param name="cmd">The convert command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created draft invoice.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the quote is not found or has no items.</exception>
    public async Task<int> HandleAsync(ConvertQuoteToInvoiceCommand cmd, CancellationToken ct)
    {
        var quote = await quoteRepo.FindByIdAsync(cmd.QuoteId, ct)
            ?? throw new InvalidOperationException($"Quote {cmd.QuoteId} not found.");

        var itemData = quote.GetInvoiceItemData();

<<<<<<< HEAD
        var invoice = Invoice.CreateDraft(quote.ClientId, quote.ExpiryDate);
=======
        var dueDate = quote.ValidUntil ?? DateTimeOffset.UtcNow.AddDays(30);
        var invoice = Invoice.Create(quote.ClientId, dueDate, isDraft: true);
>>>>>>> origin/main

        foreach (var (description, unitPrice, quantity) in itemData)
        {
            invoice.AddItem(description, unitPrice, quantity);
        }

        invoiceRepo.Add(invoice);
<<<<<<< HEAD
        quote.Accept();
=======

        quote.UpdateDetails(
            quote.Subject, QuoteStage.Accepted, quote.ValidUntil,
            quote.ProposalText, quote.CustomerNotes, quote.AdminNotes);
>>>>>>> origin/main

        await uow.SaveChangesAsync(ct);
        return invoice.Id;
    }
}
