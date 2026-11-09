using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports domain records, deduplicated by domain name, with the same status-transition mapping
/// the source system used.
/// </summary>
public sealed class DomainsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>domains</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Decides the currency an imported domain is billed in: the client's billing currency.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Names the registrar module an imported domain is managed through: the one its TLD is configured for.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Initializes a new instance of the <see cref="DomainsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>domains</c> dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="payerCurrency">Decides the currency an imported domain is billed in: the client's billing currency.</param>
    /// <param name="registrars">Names the registrar module an imported domain is managed through: the one its TLD is configured for.</param>
    public DomainsImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        IPayerCurrencyResolver payerCurrency,
        RegistrarProviderResolver registrars)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _payerCurrency = payerCurrency;
        _registrars = registrars;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Domains;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<DomainRecord>(job, EntityType, "domains", ImportDomainAsync), ct);

    /// <summary>Imports one plugin-side domain record in the status it had at the source, raising no domain events.</summary>
    /// <param name="rec">The domain record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the domain was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportDomainAsync(DomainRecord rec, CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        if (rec.RegisteredAt is not { } registeredAt || rec.ExpiresAt is not { } expiresAt)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.DateMissing);
        }

        var existing = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(x => x.Name == RegisteredDomain.NormaliseName(rec.DomainName), ct);
        if (existing is not null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        // The source record carries no currency of its own; its amounts are in the client's billing currency.
        var currency = (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code;
        var registrar = await _registrars.ResolveForTldAsync(rec.DomainName, ct);
        var domain = RegisteredDomain.Import(
            clientId: clientId.Value,
            name: rec.DomainName,
            status: MapStatus(rec.Status),
            registeredAt: registeredAt,
            expiresAt: expiresAt,
            autoRenew: rec.AutoRenew,
            priceCurrency: currency,
            registrar: rec.Registrar,
            registrarModule: registrar.Module);

        domain.Update(
            rec.FirstPaymentAmount, rec.RecurringAmount, rec.PaymentMethod,
            null, rec.SubscriptionId, rec.AdminNotes,
            expiresAt, rec.NextDueDate ?? expiresAt, 1, domain.Status);

        if (rec.Nameservers is { Count: > 0 })
        {
            domain.SetNameservers(rec.Nameservers);
        }

        _context.Domains.Add(domain);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }

    /// <summary>Maps the source system's status word to the local domain status.</summary>
    /// <param name="sourceStatus">The source's status word; null is read as active.</param>
    /// <returns>The matching <see cref="DomainStatus"/>; any word without its own state (grace, pending and the like) is <see cref="DomainStatus.Active"/>.</returns>
    private static DomainStatus MapStatus(string? sourceStatus) =>
        (sourceStatus ?? SourceDomainStatuses.Active).ToLowerInvariant() switch
        {
            SourceDomainStatuses.Expired => DomainStatus.Expired,
            SourceDomainStatuses.Redemption => DomainStatus.Redemption,
            SourceDomainStatuses.Transferred => DomainStatus.Transferred,
            SourceDomainStatuses.Cancelled or SourceDomainStatuses.Fraud => DomainStatus.Cancelled,
            _ => DomainStatus.Active,
        };
}
