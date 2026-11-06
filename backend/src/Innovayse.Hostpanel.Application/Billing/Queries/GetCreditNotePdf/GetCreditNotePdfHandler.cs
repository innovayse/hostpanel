using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNotePdf;

/// <summary>Renders a credit note as a PDF in the language of its client, with the seller read from the portal settings.</summary>
/// <remarks>
/// Serves the staff route as it is, and the client route once <c>GetMyCreditNotePdfHandler</c> has settled that the caller owns a
/// note that was issued. The PDF is printed in the client language and not the language of whoever asks, so staff and client
/// download the same document. It goes through the same renderer, fonts and render limit as an invoice.
/// </remarks>
public sealed class GetCreditNotePdfHandler
{
    /// <summary>The PDF content type.</summary>
    private const string PdfContentType = "application/pdf";

    /// <summary>Decimal places written when the currency of the note has no <c>Currencies</c> row to say otherwise.</summary>
    private const int FallbackDecimals = 2;

    /// <summary>The module data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client address and language and the seller settings.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Draws the PDF.</summary>
    private readonly IInvoicePdfRenderer _renderer;

    /// <summary>The most lines a note may have and still be rendered.</summary>
    private readonly int _maxLines;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module data context.</param>
    /// <param name="bus">The message bus, for the client address and language and the seller settings.</param>
    /// <param name="renderer">Draws the PDF.</param>
    /// <param name="options">How large a document this deployment will render.</param>
    public GetCreditNotePdfHandler(
        IHostpanelDbContext context, IMessageBus bus, IInvoicePdfRenderer renderer, IOptions<InvoicePdfOptions> options)
    {
        _context = context;
        _bus = bus;
        _renderer = renderer;
        _maxLines = options.Value.MaxLines;
    }

    /// <summary>Handles <see cref="GetCreditNotePdfQuery"/>.</summary>
    /// <param name="query">The note to render.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The PDF as <c>credit-note-{number}.pdf</c>, <c>CREDIT_NOTE_NOT_FOUND</c> when the id names no note,
    /// <c>INVOICE_PDF_EXCEEDS_LIMIT</c> when it has more lines than <c>InvoicePdf:MaxLines</c>, or
    /// <c>INVOICE_PDF_RENDER_BUSY</c> when no render slot freed within <c>InvoicePdf:MaxRenderWaitSeconds</c>.
    /// </returns>
    public async Task<Result<FileDownloadDto>> HandleAsync(GetCreditNotePdfQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var note = await _context.CreditNotes
            .AsNoTracking()
            .Include(n => n.Items)
            .FirstOrDefaultAsync(n => n.Id == query.CreditNoteId, ct);
        if (note is null)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }

        if (note.Items.Count > _maxLines)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.INVOICE_PDF_EXCEEDS_LIMIT);
        }

        var money = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == note.Currency)
            .Select(c => new InvoicePdfMoneyFormat(c.Prefix, c.Suffix, c.Decimals))
            .FirstOrDefaultAsync(ct);

        // The language is a property of the owner account, resolved together with the email address.
        var client = await _bus.GetRequiredClientBriefAsync(note.ClientId, withEmail: true, ct);
        var labels = CreditNotePdfLabelSets.For(client.Language);
        var seller = await _bus.ReadPdfSellerAsync(labels.Document.TaxId, ct);

        // A draft has no number yet; it prints its id so the document is still identifiable.
        var number = note.Number ?? $"#{note.Id}";
        var model = new CreditNotePdfModel(
            Number: number,
            Status: note.Status,
            IssuedAt: note.IssuedAt ?? note.CreatedAt,
            InvoiceId: note.InvoiceId,
            Reason: note.Reason,
            Seller: seller.Party,
            BillTo: client.ToPdfParty(),
            LogoPath: seller.LogoPath,
            Lines: note.Items.OrderBy(i => i.Id).Select(i => new InvoicePdfLine(i.Description, i.Quantity, i.UnitPrice, i.Amount)).ToList(),
            SubTotal: note.SubTotal,
            TaxRate: note.TaxRate,
            Tax: note.Tax,
            Total: note.Total,
            Money: money ?? new InvoicePdfMoneyFormat(string.Empty, " " + note.Currency, FallbackDecimals),
            Labels: labels);

        var rendered = await _renderer.RenderCreditNoteAsync(model, ct);
        if (rendered.IsFailure)
        {
            return Result<FileDownloadDto>.FailureCode(rendered.ErrorCode!);
        }

        return Result<FileDownloadDto>.Success(new FileDownloadDto(rendered.Value!, PdfContentType, $"credit-note-{note.Number ?? note.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}.pdf"));
    }
}
