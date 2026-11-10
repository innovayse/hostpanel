using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports invoice records, deduplicated per client by the source system's own invoice id, with
/// the source's tax, credit and payment moment so imported totals match what the client was billed.
/// </summary>
public sealed class InvoicesImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>invoices</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Decides the currency an invoice with no currency of its own bills in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock the import moment is read from; an overdue record becomes overdue at it.</summary>
    private readonly IClock _clock;

    /// <summary>Logs a skipped or failed record.</summary>
    private readonly ILogger<InvoicesImporter> _logger;

    /// <summary>Initializes a new instance of the <see cref="InvoicesImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>invoices</c> dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="payerCurrency">Decides the currency an invoice with no currency of its own bills in.</param>
    /// <param name="clock">The clock the import moment is read from; an overdue record becomes overdue at it.</param>
    /// <param name="logger">Logs a skipped or failed record.</param>
    public InvoicesImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        IPayerCurrencyResolver payerCurrency,
        IClock clock,
        ILogger<InvoicesImporter> logger)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _payerCurrency = payerCurrency;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Invoices;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<InvoiceRecord>(job, EntityType, "invoices", ImportInvoiceAsync), ct);

    /// <summary>Imports one plugin-side invoice record, with its line items, totals and status.</summary>
    /// <param name="rec">The invoice record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the invoice was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportInvoiceAsync(InvoiceRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (rec.Date is not { } issuedAt || rec.DueDate is not { } dueDate)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.DateMissing);
        }

        // The source invoice number is unique only within one source, so the duplicate check is
        // scoped to the client it belongs to as well.
        if (!string.IsNullOrWhiteSpace(rec.ExternalId))
        {
            var exists = await _context.Invoices.AsNoTracking()
                .AnyAsync(i => i.ClientId == clientId.Value && i.ExternalId == rec.ExternalId, ct);
            if (exists)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }
        }

        // The source record carries no currency, so the imported invoice bills in what the
        // migrated client is billed in here: their recorded currency, else the base.
        var currency = (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code;

        var invoice = rec.Status == SourceInvoiceStatuses.Draft
            ? Invoice.CreateDraft(clientId.Value, dueDate, currency, issuedAt)
            : Invoice.Create(clientId.Value, dueDate, currency, issuedAt);

        foreach (var item in rec.Items)
        {
            invoice.AddItem(item.Description, item.Amount, item.Quantity);
        }

        invoice.UpdateOptions(issuedAt, dueDate, rec.PaymentMethod, rec.TaxRate);
        invoice.ImportTotals(rec.Tax, rec.Credit);
        if (invoice.Total != rec.Total)
        {
            // The lines did not add up to the source's sub-total; the invoice is still imported
            // so nothing is lost, and the operator can find it by this line.
            _logger.LogWarning(
                "Invoice {ExternalId} imported with total {Total}, the source total is {SourceTotal}",
                rec.ExternalId, invoice.Total, rec.Total);
        }

        ApplySourceStatus(invoice, rec);

        if (!string.IsNullOrWhiteSpace(rec.ExternalId))
        {
            invoice.SetExternalId(rec.ExternalId);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }

    /// <summary>Moves a newly created invoice to the status it had in the source.</summary>
    /// <param name="invoice">The invoice being imported, Draft or Unpaid.</param>
    /// <param name="rec">The source record whose status and payment moment are applied.</param>
    private void ApplySourceStatus(Invoice invoice, InvoiceRecord rec)
    {
        // The source's payment moment; an old record that holds none was paid no later than it
        // was issued as far as anything here can tell, so the issue date stands in for it.
        var paidAt = rec.PaidAt ?? invoice.InvoiceDate;

        switch (rec.Status)
        {
            case SourceInvoiceStatuses.Paid:
                invoice.RecordPaidHistory(MigrationDefaults.PaidInvoiceReference, paidAt);
                break;
            case SourceInvoiceStatuses.Overdue:
            case SourceInvoiceStatuses.Collections:
                // There is no Collections status here; Overdue is the closest, as of the import.
                invoice.MarkOverdue(_clock.UtcNow);
                break;
            case SourceInvoiceStatuses.Cancelled:
                invoice.Cancel();
                break;
            case SourceInvoiceStatuses.Refunded:
                invoice.RecordPaidHistory(MigrationDefaults.PaidInvoiceReference, paidAt);
                invoice.Refund();
                break;
            default:
                // Draft and Unpaid already hold from creation; PaymentPending (payment started but
                // not confirmed) stays Unpaid, as does a word this panel does not know.
                break;
        }
    }
}
