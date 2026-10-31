using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports client service records, deduplicated by client and domain (or panel username), matched against this
/// install's own products by name.
/// </summary>
public sealed class ServicesImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>services</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Resolves each record's client email to a local client id.</summary>
    private readonly MigrationClientResolver _clientResolver;

    /// <summary>Loads the products a record's product name is matched against.</summary>
    private readonly MigrationProductLookup _productLookup;

    /// <summary>Reads the moment each service is imported, which also anchors the billing period of a record the source gives no registration date.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="ServicesImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>services</c> dataset and records each record's outcome.</param>
    /// <param name="clientResolver">Resolves each record's client email to a local client id.</param>
    /// <param name="productLookup">Loads the products a record's product name is matched against.</param>
    /// <param name="clock">Reads the moment each service is imported, which also anchors the billing period of a record the source gives no registration date.</param>
    public ServicesImporter(
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        MigrationClientResolver clientResolver,
        MigrationProductLookup productLookup,
        IClock clock)
    {
        _context = context;
        _runner = runner;
        _clientResolver = clientResolver;
        _productLookup = productLookup;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Services;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        var allProducts = await _productLookup.LoadAllProductsAsync(ct);

        await _runner.RunAsync(
            new MigrationRecordRun<ServiceRecord>(
                job, EntityType, "services", (rec, token) => ImportServiceAsync(rec, allProducts, token)),
            ct);
    }

    /// <summary>Imports one plugin-side service record, resolving its client and product first.</summary>
    /// <param name="rec">The service record read from the source plugin.</param>
    /// <param name="allProducts">Every product configured here, for matching the record's product name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether the service was imported or skipped, and why when skipped.</returns>
    private async Task<RecordImportResult> ImportServiceAsync(
        ServiceRecord rec,
        IReadOnlyList<Product> allProducts,
        CancellationToken ct)
    {
        var clientId = await _clientResolver.ResolveClientIdAsync(rec.ClientEmail, ct);
        if (clientId is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ClientNotFound);
        }

        // The source sends no service id of its own, so a service is matched by what identifies
        // its hosting account for the client: the domain, else the panel username.
        if (await ExistsAsync(clientId.Value, rec, ct))
        {
            return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
        }

        if (allProducts.Count == 0)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.NoProducts);
        }

        // No fallback to another product: a service linked to the wrong product would bill and
        // provision as that product without anyone noticing.
        var product = MigrationProductLookup.FindByName(allProducts, rec.ProductName);
        if (product is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.ProductNotFound);
        }

        // A cycle this version cannot bill (triennial, one-time and the like) is skipped rather than
        // stored as a guess: a service on the wrong cycle would be invoiced for the wrong amount of time.
        if (!BillingCycleParser.TryParse(rec.BillingCycle, out var billingCycle))
        {
            return RecordImportResult.Skipped(MigrationSkipReason.BillingCycleUnsupported);
        }

        var now = _clock.UtcNow;

        // The source does not record when a service was suspended, nor always when it ended, so
        // the moment of the import stands in for what it does not say.
        var svc = ClientService.Import(new ServiceImport(
            ClientId: clientId.Value,
            ProductId: product.Id,
            BillingCycle: billingCycle,
            Status: ToStatus(rec.Status),
            RegisteredAt: rec.RegisteredAt ?? now,
            StatusChangedAt: rec.TerminatedAt ?? now,
            SuspensionSource: rec.Status == SourceServiceStatuses.SuspendedByAdmin ? SuspensionSource.Manual : SuspensionSource.Other,
            HostingAccount: new ServiceHostingAccount(
                ServerId: null, Domain: rec.Domain, ProvisioningRef: rec.Username, DedicatedIp: null, Username: rec.Username, Password: null)));
        svc.UpdateAdminNotes(rec.AdminNotes);

        // Applied after the import: Import anchors a renewal date of its own, and the source's
        // next due date is the one the client is really billed on, so it wins when the source has one.
        svc.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: billingCycle,
            Quantity: 1,
            FirstPaymentAmount: rec.FirstPaymentAmount,
            RecurringAmount: rec.RecurringAmount,
            NextRenewalAt: rec.NextDueDate ?? svc.NextRenewalAt,
            PaymentMethod: rec.PaymentMethod,
            PromotionCode: null,
            SubscriptionId: rec.SubscriptionId));

        _context.ClientServices.Add(svc);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }

    /// <summary>Whether the client already has the service the record describes, matched by domain, else by panel username.</summary>
    /// <param name="clientId">The local client the record belongs to.</param>
    /// <param name="rec">The service record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when a matching service exists; <see langword="false"/> as well when the record carries neither value.</returns>
    private async Task<bool> ExistsAsync(int clientId, ServiceRecord rec, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(rec.Domain))
        {
            var domainLower = rec.Domain.ToLower();
            return await _context.ClientServices.AnyAsync(
                s => s.ClientId == clientId && s.Domain != null && s.Domain.ToLower() == domainLower, ct);
        }

        if (!string.IsNullOrWhiteSpace(rec.Username))
        {
            return await _context.ClientServices.AnyAsync(
                s => s.ClientId == clientId && s.Username == rec.Username, ct);
        }

        return false;
    }

    /// <summary>Maps the source's status word to the local status; a word not named here (pending, fraud) stays pending.</summary>
    /// <param name="sourceStatus">The source's status word.</param>
    /// <returns>The local status.</returns>
    private static ServiceStatus ToStatus(string sourceStatus) => sourceStatus switch
    {
        SourceServiceStatuses.Active => ServiceStatus.Active,
        SourceServiceStatuses.Suspended or SourceServiceStatuses.SuspendedByAdmin => ServiceStatus.Suspended,
        SourceServiceStatuses.Terminated or SourceServiceStatuses.Cancelled => ServiceStatus.Terminated,
        _ => ServiceStatus.Pending,
    };
}
