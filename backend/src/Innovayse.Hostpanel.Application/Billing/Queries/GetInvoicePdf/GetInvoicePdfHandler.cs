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

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetInvoicePdf;

/// <summary>Renders an invoice as a PDF, with the seller read from the portal settings.</summary>
/// <remarks>
/// Serves the staff route as it is, and the client route once <c>GetMyInvoicePdfHandler</c> has
/// settled that the caller owns a published invoice. It prints whatever invoice it is given,
/// drafts included; keeping drafts from clients is the ownership rule's job. It reads invoices and
/// currencies, which are Billing's own; the client's address and language, the seller's settings and the bank details
/// belong to other modules and are asked for over the bus. The document is printed in the language of the client, so staff
/// and client download the same document.
/// </remarks>
public sealed class GetInvoicePdfHandler
{
    /// <summary>The PDF's content type.</summary>
    private const string PdfContentType = "application/pdf";

    /// <summary>Decimal places written when the invoice's currency has no <c>Currencies</c> row to say otherwise.</summary>
    private const int FallbackDecimals = 2;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client's address and language, the seller's settings and the bank details.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Draws the PDF.</summary>
    private readonly IInvoicePdfRenderer _renderer;

    /// <summary>Where the client portal is, for the link to the invoice page.</summary>
    private readonly ClientPortalOptions _clientPortal;

    /// <summary>The most lines an invoice may have and still be rendered.</summary>
    private readonly int _maxLines;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client's address and language, the seller's settings and the bank details.</param>
    /// <param name="renderer">Draws the PDF.</param>
    /// <param name="options">How large an invoice this deployment will render.</param>
    /// <param name="clientPortal">Where the client portal is, for the link to the invoice page.</param>
    public GetInvoicePdfHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IInvoicePdfRenderer renderer,
        IOptions<InvoicePdfOptions> options,
        IOptions<ClientPortalOptions> clientPortal)
    {
        _context = context;
        _bus = bus;
        _renderer = renderer;
        _maxLines = options.Value.MaxLines;
        _clientPortal = clientPortal.Value;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The invoice to render.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The PDF as <c>invoice-{id}.pdf</c>, <c>INVOICE_NOT_FOUND</c> when the id names no invoice,
    /// <c>INVOICE_PDF_EXCEEDS_LIMIT</c> when it has more lines than <c>InvoicePdf:MaxLines</c>, or
    /// <c>INVOICE_PDF_RENDER_BUSY</c> when no render slot freed within <c>InvoicePdf:MaxRenderWaitSeconds</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">The invoice names a client that does not exist, so the database is broken.</exception>
    public async Task<Result<FileDownloadDto>> HandleAsync(GetInvoicePdfQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // The invoice comes with its payments, which the amounts paid and due are worked out from. The lines are a
        // second, bounded read: they stop one past the ceiling, which is enough to tell an invoice over it without
        // reading all of its lines.
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, ct);
        if (invoice is null)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        var lines = await _context.InvoiceItems
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .OrderBy(item => item.Id)
            .Take(_maxLines + 1)
            .Select(item => new InvoicePdfLine(item.Description, item.Quantity, item.UnitPrice, item.Amount))
            .ToListAsync(ct);
        if (lines.Count > _maxLines)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.INVOICE_PDF_EXCEEDS_LIMIT);
        }

        var money = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == invoice.Currency)
            .Select(c => new InvoicePdfMoneyFormat(c.Prefix, c.Suffix, c.Decimals, c.Code))
            .FirstOrDefaultAsync(ct);

        // FK_Invoices_Clients_ClientId restricts deleting a client that has invoices, so a missing
        // client is a broken database, not an invoice to print without an addressee. The language is a
        // property of the owner account, resolved together with the email address.
        var client = await _bus.GetRequiredClientBriefAsync(invoice.ClientId, withEmail: true, ct);
        var labels = DocumentPdfLabelSets.For(client.Language);
        var seller = await _bus.ReadPdfSellerAsync(labels, ct);

        // Only an invoice that can still be paid points to the payment page and the bank account.
        BankTransferDetailsDto? bankTransfer = null;
        if (invoice.IsPayable)
        {
            var settings = await _bus.LoadBankTransferSettingsAsync(ct);
            bankTransfer = settings is { IsEnabled: true, HasPayerDetails: true } ? settings.ToDetails(invoice) : null;
        }

        var model = new InvoicePdfModel(
            InvoiceId: invoice.Id,
            Status: invoice.Status,
            InvoiceDate: invoice.InvoiceDate,
            DueDate: invoice.DueDate,
            Seller: seller,
            BillTo: client.ToPdfParty(labels),
            Lines: lines,
            SubTotal: invoice.SubTotal,
            TaxRate: invoice.TaxRate,
            Tax: invoice.Tax,
            ClientVatNumber: invoice.ReverseCharge ? invoice.ClientVatNumber : null,
            Credit: invoice.Credit,
            Total: invoice.Total,
            AmountPaid: invoice.AmountPaid,
            AmountDue: invoice.AmountDue,
            // A currency with no row still prints, with its code after each amount so the reader
            // can tell which currency the figures are in.
            Money: money ?? new InvoicePdfMoneyFormat(string.Empty, " " + invoice.Currency, FallbackDecimals, invoice.Currency),
            PayUrl: invoice.IsPayable ? _clientPortal.InvoiceUrl(invoice.Id) : null,
            BankTransfer: bankTransfer,
            Labels: labels);

        var rendered = await _renderer.RenderAsync(model, ct);
        if (rendered.IsFailure)
        {
            return Result<FileDownloadDto>.FailureCode(rendered.ErrorCode!);
        }

        return Result<FileDownloadDto>.Success(new FileDownloadDto(rendered.Value!, PdfContentType, $"invoice-{invoice.Id}.pdf"));
    }
}
