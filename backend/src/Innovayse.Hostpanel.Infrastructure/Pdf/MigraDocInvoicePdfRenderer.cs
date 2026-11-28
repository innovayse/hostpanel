using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// Draws invoices and credit notes as A4 PDFs, the adapter of <see cref="IInvoicePdfRenderer"/>: it takes a render slot, reads
/// the logo and hands the document to <see cref="PdfDocumentLayout"/>, which lays both documents out the same way with MigraDoc,
/// rendered through PDFsharp.
/// </summary>
/// <remarks>
/// The logo is read only from the branding upload directory under the web root. A
/// <c>portal.logo</c> that is a URL, or a path that leads anywhere else, is left out: rendering
/// never reaches the network and never opens a file outside that directory.
/// </remarks>
public sealed class MigraDocInvoicePdfRenderer : IInvoicePdfRenderer
{
    /// <summary>Largest width the logo is drawn at.</summary>
    private const double LogoMaxWidth = 120;

    /// <summary>Largest height the logo is drawn at.</summary>
    private const double LogoMaxHeight = 40;

    /// <summary>Where the web root is, for finding an uploaded logo.</summary>
    private readonly IWebHostEnvironment _env;

    /// <summary>Where branding uploads live under the web root.</summary>
    private readonly BrandingOptions _branding;

    /// <summary>The process-wide limit on renders in flight.</summary>
    private readonly InvoicePdfRenderGate _gate;

    /// <summary>Logs why a configured logo was left out, and a render turned away as busy.</summary>
    private readonly ILogger<MigraDocInvoicePdfRenderer> _logger;

    /// <summary>Initializes the renderer.</summary>
    /// <param name="env">Where the web root is, for finding an uploaded logo.</param>
    /// <param name="branding">Where branding uploads live under the web root.</param>
    /// <param name="gate">The process-wide limit on renders in flight.</param>
    /// <param name="logger">Logs why a configured logo was left out, and a render turned away as busy.</param>
    public MigraDocInvoicePdfRenderer(
        IWebHostEnvironment env,
        IOptions<BrandingOptions> branding,
        InvoicePdfRenderGate gate,
        ILogger<MigraDocInvoicePdfRenderer> logger)
    {
        _env = env;
        _branding = branding.Value;
        _gate = gate;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Waits for a slot in <see cref="InvoicePdfRenderGate"/> first, then renders inline and holds
    /// the slot until the bytes are written; a render that throws gives its slot back too. When this
    /// request gets no slot within the gate's wait, nothing is rendered and the result is
    /// <c>INVOICE_PDF_RENDER_BUSY</c>, logged at Warning so a deployment that keeps hitting the cap
    /// can see it.
    /// </remarks>
    public Task<Result<byte[]>> RenderAsync(InvoicePdfModel model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderInSlotAsync(
            () => PdfDocumentLayout.Render(PdfDocumentContents.ForInvoice(model), LoadLogo(model.Seller.LogoPath)),
            ct);
    }

    /// <inheritdoc/>
    /// <remarks>Takes a slot in the same gate as an invoice render, so credit notes and invoices share one limit.</remarks>
    public Task<Result<byte[]>> RenderCreditNoteAsync(CreditNotePdfModel model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        return RenderInSlotAsync(
            () => PdfDocumentLayout.Render(PdfDocumentContents.ForCreditNote(model), LoadLogo(model.Seller.LogoPath)),
            ct);
    }

    /// <summary>Waits for a render slot, then runs one render inside it.</summary>
    /// <param name="render">Draws the document; runs only once a slot is held.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PDF bytes, or <c>INVOICE_PDF_RENDER_BUSY</c> when no slot came free in time.</returns>
    private async Task<Result<byte[]>> RenderInSlotAsync(Func<byte[]> render, CancellationToken ct)
    {
        using var slot = await _gate.TryEnterAsync(ct);
        if (slot is null)
        {
            _logger.LogWarning(
                "Invoice PDF render refused as busy: it waited {WaitSeconds}s and no slot came free, with {Limit} render(s) allowed at once. Raise InvoicePdf__MaxConcurrentRenders if this keeps happening.",
                _gate.MaxWait.TotalSeconds,
                _gate.Limit);
            return Result<byte[]>.FailureCode(HostpanelErrorCodes.INVOICE_PDF_RENDER_BUSY);
        }

        return Result<byte[]>.Success(render());
    }

    /// <summary>
    /// Reads the logo when <paramref name="logoPath"/> names an image inside the branding upload
    /// directory; otherwise logs why at Debug and returns null.
    /// </summary>
    /// <param name="logoPath">The <c>portal.logo</c> value as stored.</param>
    /// <returns>The logo and the size to draw it at, or null to print without a logo.</returns>
    private PdfLogo? LoadLogo(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return null;
        }

        if (Uri.TryCreate(logoPath, UriKind.Absolute, out var uri) && uri.Scheme is not "file")
        {
            _logger.LogDebug("Invoice PDF logo skipped: {LogoPath} is not a local upload.", logoPath);
            return null;
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var baseDir = Path.GetFullPath(Path.Combine(webRoot, _branding.RelativePath)) + Path.DirectorySeparatorChar;
        var relative = logoPath.Split('?', 2)[0].TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(webRoot, relative));
        if (!full.StartsWith(baseDir, StringComparison.Ordinal) || !File.Exists(full))
        {
            _logger.LogDebug("Invoice PDF logo skipped: {LogoPath} is not a file in the branding directory.", logoPath);
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(full);
            using var image = XImage.FromStream(new MemoryStream(bytes));
            var scale = Math.Min(1, Math.Min(LogoMaxWidth / image.PixelWidth, LogoMaxHeight / image.PixelHeight));
            return new PdfLogo(bytes, image.PixelWidth * scale, image.PixelHeight * scale);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Invoice PDF logo skipped: {LogoPath} could not be read or decoded.", logoPath);
            return null;
        }
    }
}
