using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Enums;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports client ledger transaction records, deduplicated per client by the source system's
/// transaction reference, each linked to the invoice imported from the source invoice it pays.
/// </summary>
public sealed class TransactionsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>transactions</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Decides the currency a record with no currency of its own is in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes a new instance of the <see cref="TransactionsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>transactions</c> dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="payerCurrency">Decides the currency a record with no currency of its own is in.</param>
    public TransactionsImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _payerCurrency = payerCurrency;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Transactions;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<TransactionRecord>(job, EntityType, "transactions", ImportTransactionAsync), ct);

    /// <summary>
    /// Imports one plugin-side transaction, linking it to the imported invoice it pays.
    /// </summary>
    /// <param name="rec">The transaction record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the transaction was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportTransactionAsync(TransactionRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (rec.Date is not { } date)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.DateMissing);
        }

        // The source sends its own invoice number; the ledger row links to the invoice imported
        // from it, which carries that number as its external id. A payment for an invoice that was
        // not imported is skipped rather than linked to an unrelated local invoice, so a later
        // pull that imports the invoice first picks it up.
        int? invoiceId = null;
        if (!string.IsNullOrWhiteSpace(rec.InvoiceId))
        {
            invoiceId = await _context.Invoices.AsNoTracking()
                .Where(i => i.ClientId == clientId.Value && i.ExternalId == rec.InvoiceId)
                .Select(i => (int?)i.Id)
                .FirstOrDefaultAsync(ct);
            if (invoiceId is null)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.InvoiceNotFound);
            }
        }

        var exists = await _context.Transactions.AsNoTracking()
            .AnyAsync(t => t.ClientId == clientId.Value && t.TransactionId == rec.TransactionId, ct);
        if (exists)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // A record without a currency is in what the migrated client is billed in.
        var currency = string.IsNullOrWhiteSpace(rec.Currency)
            ? (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code
            : rec.Currency;
        var transaction = Transaction.Create(
            clientId.Value,
            date,
            rec.Description,
            rec.TransactionId,
            invoiceId,
            rec.PaymentMethod,
            rec.AmountIn,
            rec.AmountOut,
            rec.Fees,
            false,
            currency,
            TransactionSource.Import);

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
