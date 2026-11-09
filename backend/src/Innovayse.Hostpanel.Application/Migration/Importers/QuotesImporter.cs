using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports quote records, deduplicated by client and subject.
/// </summary>
public sealed class QuotesImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Decides the currency a quote with no currency of its own is in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Initializes a new instance of the <see cref="QuotesImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="payerCurrency">Decides the currency a quote with no currency of its own is in.</param>
    public QuotesImporter(
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
    public MigrationEntityType EntityType => MigrationEntityType.Quotes;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<QuoteRecord>(job, EntityType, "quotes", ImportQuoteAsync), ct);

    /// <summary>Imports one plugin-side quote record.</summary>
    /// <param name="rec">The quote record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the quote was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportQuoteAsync(QuoteRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (rec.ExpiryDate is not { } expiryDate)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.DateMissing);
        }

        var existingQuote = await _context.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.ClientId == clientId.Value && q.Subject.ToLower() == rec.Subject.ToLower(), ct);
        if (existingQuote is not null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // The source record carries no currency, so the quote is in what the migrated client is billed in.
        var currency = (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code;
        var quote = Quote.Create(clientId.Value, rec.Subject, expiryDate, currency,
            proposalText: rec.ProposalText,
            customerNotes: rec.CustomerNotes);

        foreach (var item in rec.Items)
        {
            quote.AddItem(item.Description, item.UnitPrice, item.Quantity, item.DiscountPercent);
        }

        switch (rec.Stage)
        {
            case SourceQuoteStages.Delivered:
                quote.Deliver();
                break;
            case SourceQuoteStages.Accepted:
                quote.Accept();
                break;
            case SourceQuoteStages.Lost:
                quote.MarkLost();
                break;
            case SourceQuoteStages.Dead:
                quote.MarkDead();
                break;
        }

        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }
}
