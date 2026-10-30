using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Draws an invoice as a PDF document.</summary>
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
}
