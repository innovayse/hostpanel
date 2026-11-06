using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Draws the panel documents, an invoice and a credit note, as PDF files.</summary>
/// <remarks>
/// Named for the work rather than the library, like <see cref="IBrandingImageProcessor"/>. The
/// implementation must not reach the network or depend on fonts installed on the host: the
/// production image has none.
/// </remarks>
public interface IInvoicePdfRenderer
{
    /// <summary>Renders the invoice.</summary>
    /// <param name="model">Everything the PDF prints, already resolved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The PDF file's bytes, or <c>INVOICE_PDF_RENDER_BUSY</c> when the renderer is at capacity and no
    /// slot came free for this render within the wait it allows.
    /// </returns>
    public Task<Result<byte[]>> RenderAsync(InvoicePdfModel model, CancellationToken ct);

    /// <summary>Renders a credit note in the language of its labels, with the same fonts and the same render limit as an invoice.</summary>
    /// <param name="model">Everything the PDF prints, already resolved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The PDF file's bytes, or <c>INVOICE_PDF_RENDER_BUSY</c> when the renderer is at capacity and no
    /// slot came free for this render within the wait it allows.
    /// </returns>
    public Task<Result<byte[]>> RenderCreditNoteAsync(CreditNotePdfModel model, CancellationToken ct);
}
