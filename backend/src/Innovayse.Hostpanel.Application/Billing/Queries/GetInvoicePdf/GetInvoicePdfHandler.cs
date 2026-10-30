using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetSettingValues;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Domain.Settings;
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
/// currencies, which are Billing's own; the client's address and the seller's settings belong to
/// other modules and are asked for over the bus.
/// </remarks>
public sealed class GetInvoicePdfHandler
{
    /// <summary>The PDF's content type.</summary>
    private const string PdfContentType = "application/pdf";

    /// <summary>Decimal places written when the invoice's currency has no <c>Currencies</c> row to say otherwise.</summary>
    private const int FallbackDecimals = 2;

    /// <summary>The portal settings the seller block and logo are read from.</summary>
    private static readonly List<string> SellerKeys =
        [PortalSettingKeys.SiteName, PortalSettingKeys.ContactAddress, PortalSettingKeys.LegalTaxId, PortalSettingKeys.Logo];

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client's address and the seller's settings.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Draws the PDF.</summary>
    private readonly IInvoicePdfRenderer _renderer;

    /// <summary>The most lines an invoice may have and still be rendered.</summary>
    private readonly int _maxLines;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client's address and the seller's settings.</param>
    /// <param name="renderer">Draws the PDF.</param>
    /// <param name="options">How large an invoice this deployment will render.</param>
    public GetInvoicePdfHandler(IHostpanelDbContext context, IMessageBus bus, IInvoicePdfRenderer renderer, IOptions<InvoicePdfOptions> options)
    {
        _context = context;
        _bus = bus;
        _renderer = renderer;
        _maxLines = options.Value.MaxLines;
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

        // One statement: the line items join in and the currency row comes back as a correlated
        // subquery. AsSingleQuery() overrides the context's split-query default, which would
        // otherwise send the items as a second round trip. The items stop one past the ceiling,
        // which is enough to tell an invoice over it without reading all of its lines.
        var invoice = await _context.Invoices
            .AsNoTracking()
            .AsSingleQuery()
            .Where(i => i.Id == query.InvoiceId)
            .Select(i => new
            {
                i.Id,
                i.ClientId,
                i.Status,
                i.InvoiceDate,
                i.DueDate,
                i.PaidAt,
                i.SubTotal,
                i.TaxRate,
                i.Tax,
                i.Credit,
                i.Total,
                i.Currency,
                Lines = i.Items
                    .OrderBy(item => item.Id)
                    .Take(_maxLines + 1)
                    .Select(item => new InvoicePdfLine(item.Description, item.Quantity, item.UnitPrice, item.Amount))
                    .ToList(),
                Money = _context.Currencies
                    .Where(c => c.Code == i.Currency)
                    .Select(c => new InvoicePdfMoneyFormat(c.Prefix, c.Suffix, c.Decimals))
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (invoice is null)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (invoice.Lines.Count > _maxLines)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.INVOICE_PDF_EXCEEDS_LIMIT);
        }

        // FK_Invoices_Clients_ClientId restricts deleting a client that has invoices, so a missing
        // client is a broken database, not an invoice to print without an addressee.
        var client = await _bus.GetRequiredClientBriefAsync(invoice.ClientId, withEmail: false, ct);

        var rows = await _bus.InvokeAsync<Result<IReadOnlyList<SettingDto>>>(new GetSettingValuesQuery(SellerKeys), ct);
        var settings = (rows.Value ?? [])
            .Where(s => s.Value is not null)
            .ToDictionary(s => s.Key, s => s.Value!.Trim(), StringComparer.Ordinal);
        string Setting(string key) => settings.GetValueOrDefault(key) ?? string.Empty;

        var sellerName = Setting(PortalSettingKeys.SiteName);
        var taxId = Setting(PortalSettingKeys.LegalTaxId);
        var sellerLines = Setting(PortalSettingKeys.ContactAddress)
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (taxId.Length > 0)
        {
            sellerLines.Add($"Tax ID: {taxId}");
        }

        var billTo = new InvoicePdfParty(
            client.FullName,
            NonEmpty(
                client.CompanyName,
                client.Address.Street,
                client.Address.Address2,
                $"{client.Address.PostCode} {client.Address.City}".Trim(),
                client.Address.State,
                client.Address.Country));

        var logo = Setting(PortalSettingKeys.Logo);
        var model = new InvoicePdfModel(
            InvoiceId: invoice.Id,
            Status: invoice.Status,
            InvoiceDate: invoice.InvoiceDate,
            DueDate: invoice.DueDate,
            PaidAt: invoice.PaidAt,
            Seller: new InvoicePdfParty(sellerName.Length > 0 ? sellerName : EmailBranding.DefaultSiteName, sellerLines),
            BillTo: billTo,
            LogoPath: logo.Length > 0 ? logo : null,
            Lines: invoice.Lines,
            SubTotal: invoice.SubTotal,
            TaxRate: invoice.TaxRate,
            Tax: invoice.Tax,
            Credit: invoice.Credit,
            Total: invoice.Total,
            // A currency with no row still prints, with its code after each amount so the reader
            // can tell which currency the figures are in.
            Money: invoice.Money ?? new InvoicePdfMoneyFormat(string.Empty, " " + invoice.Currency, FallbackDecimals));

        var rendered = await _renderer.RenderAsync(model, ct);
        if (rendered.IsFailure)
        {
            return Result<FileDownloadDto>.FailureCode(rendered.ErrorCode!);
        }

        return Result<FileDownloadDto>.Success(new FileDownloadDto(rendered.Value!, PdfContentType, $"invoice-{invoice.Id}.pdf"));
    }

    /// <summary>The values that are not null or blank, trimmed, in order.</summary>
    /// <param name="values">Candidate address lines.</param>
    /// <returns>The lines worth printing.</returns>
    private static List<string> NonEmpty(params string?[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
}
