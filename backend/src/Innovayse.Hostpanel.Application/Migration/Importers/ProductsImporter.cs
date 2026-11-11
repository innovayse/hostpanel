using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports product groups (creating any this install does not already have, matched by name),
/// then products deduplicated by name against this install's base currency.
/// </summary>
public sealed class ProductsImporter : IMigrationEntityImporter
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>product_groups</c> and <c>products</c> datasets.</summary>
    private readonly MigrationRecordPager _pager;

    /// <summary>Pages through each record dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Loads the products a record's name is deduplicated against.</summary>
    private readonly MigrationProductLookup _productLookup;

    /// <summary>Decides the base currency an imported product prices into.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the moment each product is imported.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="ProductsImporter"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pager">Pages through the source plugin's <c>product_groups</c> and <c>products</c> datasets.</param>
    /// <param name="runner">Pages through each record dataset and records each record's outcome.</param>
    /// <param name="productLookup">Loads the products a record's name is deduplicated against.</param>
    /// <param name="payerCurrency">Decides the base currency an imported product prices into.</param>
    /// <param name="clock">Reads the moment each product is imported.</param>
    public ProductsImporter(
        IHostpanelDbContext context,
        MigrationRecordPager pager,
        MigrationRecordRunner runner,
        MigrationProductLookup productLookup,
        IPayerCurrencyResolver payerCurrency,
        IClock clock)
    {
        _context = context;
        _pager = pager;
        _runner = runner;
        _productLookup = productLookup;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Products;

    /// <inheritdoc/>
    public async Task ImportAsync(MigrationJob job, CancellationToken ct)
    {
        // Step 1: fetch all product groups, create missing ones, build sourceId→localId map
        var localGroups = await _context.ProductGroups
            .AsNoTracking()
            .Include(g => g.Products)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
        var groupIdMap = new Dictionary<int, int>(); // sourceId → localGroupId

        await foreach (var page in _pager.PagesAsync<ProductGroupRecord>(job, "product_groups", ct))
        {
            foreach (var grp in page)
            {
                var existing = localGroups.FirstOrDefault(g =>
                    string.Equals(g.Name, grp.Name, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    groupIdMap[grp.Id] = existing.Id;
                }
                else
                {
                    var newGroup = ProductGroup.Create(grp.Name, grp.Description);
                    _context.ProductGroups.Add(newGroup);
                    await _context.SaveChangesAsync(ct);
                    groupIdMap[grp.Id] = newGroup.Id;
                }
            }
        }

        // Step 2: page through products
        var allProducts = await _productLookup.LoadAllProductsAsync(ct);
        var baseCurrency = (await _payerCurrency.BaseAsync(ct)).Code;

        await _runner.RunAsync(new MigrationRecordRun<ProductRecord>(job, EntityType, "products", ImportProductAsync), ct);

        // One record of the dataset; the runner counts, logs and isolates its outcome.
        async Task<RecordImportResult> ImportProductAsync(ProductRecord rec, CancellationToken ct)
        {
            if (MigrationProductLookup.FindByName(allProducts, rec.Name) is not null)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }

            if (!groupIdMap.TryGetValue(rec.GroupId, out var localGroupId))
            {
                return RecordImportResult.Skipped(MigrationSkipReason.ProductGroupNotFound);
            }

            // The source sends its prices in its default currency, so its two figures become
            // the product's prices in this install's base currency. A cycle the source does not
            // sell gets no price row, so it is not offered here either. The legacy columns get
            // the same figures, zero for an unsold cycle, for the rollback release.
            var product = Product.Create(
                localGroupId,
                rec.Name,
                rec.Description,
                null,
                null,
                null,
                MapProductType(rec.Type),
                rec.MonthlyPrice ?? 0m,
                rec.AnnualPrice ?? 0m,
                _clock.UtcNow);
            if (rec.MonthlyPrice is { } monthlyPrice)
            {
                product.SetPrice(baseCurrency, BillingCycle.Monthly, monthlyPrice);
            }

            if (rec.AnnualPrice is { } annualPrice)
            {
                product.SetPrice(baseCurrency, BillingCycle.Annual, annualPrice);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync(ct);
            return RecordImportResult.Imported;
        }
    }

    /// <summary>
    /// Maps a source-system product `type` column value to the closest <see cref="ProductType"/>.
    /// The source system's type is a provisioning-module category, not a marketing category, so this
    /// only recognizes the one value with an unambiguous match; everything else, custom
    /// SaaS products, "other", "server", "reselleraccount", stays <see cref="ProductType.Other"/>
    /// rather than guessing at Vps/Dedicated/Ssl/Domain from a string that doesn't carry that
    /// information.
    /// </summary>
    /// <param name="sourceType">The source system's product type column value.</param>
    /// <returns>The matching product type, or <see cref="ProductType.Other"/>.</returns>
    private static ProductType MapProductType(string sourceType) => sourceType switch
    {
        SourceProductTypes.HostingAccount => ProductType.SharedHosting,
        _ => ProductType.Other,
    };
}
