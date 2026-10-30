using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Products.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// A product in the catalogue that clients can order as a service.
/// Belongs to a <see cref="ProductGroup"/>.
/// Stored in the <c>Products</c> table.
/// </summary>
public sealed class Product : BaseEntity, IAggregateRoot
{
    /// <summary>Longest a website URL may be: matches the <c>Products.Website</c> column.</summary>
    public const int WebsiteMaxLength = 500;

    /// <summary>The grace period a product starts with, in days.</summary>
    public const int DefaultGracePeriodDays = 7;

    /// <summary>The shortest grace period accepted, in days: none at all.</summary>
    public const int MinGracePeriodDays = 0;

    /// <summary>The longest grace period accepted, in days.</summary>
    public const int MaxGracePeriodDays = 365;

    /// <summary>
    /// The largest metadata bag accepted, in UTF-8 bytes of the text as sent (16 KB). The bag tells a
    /// fulfilment plugin what to deliver; anything larger is content, not a delivery hint.
    /// </summary>
    public const int MaxMetadataBytes = 16 * 1024;

    /// <summary>Gets the FK to the <see cref="ProductGroup"/> this product belongs to.</summary>
    public int GroupId { get; private set; }

    /// <summary>Gets the product display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the optional website URL for this product's landing page.</summary>
    public string? Website { get; private set; }

    /// <summary>Gets the optional URL-friendly slug for this product.</summary>
    public string? Slug { get; private set; }

    /// <summary>Gets the optional hosting package name used for provisioning (e.g. "starter", "pro").</summary>
    public string? PackageName { get; private set; }

    /// <summary>Gets the product type (hosting, VPS, domain, etc.).</summary>
    public ProductType Type { get; private set; }

    /// <summary>Gets the current status.</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>Gets the monthly price.</summary>
    public decimal MonthlyPrice { get; private set; }

    /// <summary>Gets the annual price.</summary>
    public decimal AnnualPrice { get; private set; }

    /// <summary>Gets the optional FK to the server group used for provisioning.</summary>
    public int? ServerGroupId { get; private set; }

    /// <summary>
    /// Gets whether the operator marked this product as the group's recommended plan. The storefront
    /// shows a "Recommended" badge and a highlighted card for it. At most one product per group
    /// should carry the flag: the create and update handlers clear it on the group's other products
    /// when it is set here, and the storefront falls back to the first featured product in
    /// <see cref="SortOrder"/> order if legacy data ever holds more than one.
    /// </summary>
    public bool IsFeatured { get; private set; }

    /// <summary>
    /// Gets the operator-defined position inside the group, lowest first. Ties fall back to the
    /// product name, so the default of <c>0</c> keeps the previous alphabetical order.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Gets the optional stock-keeping unit: the stable machine identifier a fulfilment plugin keys
    /// on. Always stored normalised (see <see cref="ProductSku.Normalize"/>); unique across products,
    /// which the handler checks because the rule spans aggregates.
    /// </summary>
    public string? Sku { get; private set; }

    /// <summary>
    /// Gets the optional free metadata bag, a JSON object kept in compact form, that tells a
    /// fulfilment plugin what to deliver (a licence tier, a credit amount, a plan code).
    /// </summary>
    public string? Metadata { get; private set; }

    /// <summary>
    /// Gets how many days a service of this product stays usable after its due date before it is
    /// suspended, from <see cref="MinGracePeriodDays"/> to <see cref="MaxGracePeriodDays"/>.
    /// </summary>
    public int GracePeriodDays { get; private set; } = DefaultGracePeriodDays;

    /// <summary>Gets the UTC timestamp when the product was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Backing list for per-currency prices.</summary>
    private readonly List<ProductPrice> _prices = [];

    /// <summary>Gets the stored prices, one per currency and cycle.</summary>
    /// <remarks>
    /// <see cref="MonthlyPrice"/> and <see cref="AnnualPrice"/> are the pre-multi-currency
    /// columns. They are kept for one release so a rollback has data to read: the handlers keep
    /// them mirroring the base-currency rows here, and the migration copied them into USD rows.
    /// </remarks>
    public IReadOnlyList<ProductPrice> Prices => _prices.AsReadOnly();

    /// <summary>
    /// Gets whether the product is a digital good: a fulfilment plugin delivers it and nothing is
    /// provisioned on a hosting account.
    /// </summary>
    public bool IsDigital => Type == ProductType.Digital;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Product() { }

    /// <summary>
    /// Creates a new active product and raises <see cref="ProductCreatedEvent"/>.
    /// </summary>
    /// <param name="groupId">FK to the parent product group.</param>
    /// <param name="name">Product display name.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="website">Optional website URL for the product's landing page.</param>
    /// <param name="slug">Optional URL-friendly slug for the product.</param>
    /// <param name="packageName">Optional hosting package name used for provisioning.</param>
    /// <param name="type">Product type.</param>
    /// <param name="monthlyPrice">Monthly price (≥ 0).</param>
    /// <param name="annualPrice">Annual price (≥ 0).</param>
    /// <param name="now">The current moment (UTC), read by the caller; stamped on the domain event.</param>
    /// <param name="serverGroupId">Optional FK to the server group for provisioning.</param>
    /// <param name="isFeatured">Whether the product is the group's recommended plan.</param>
    /// <param name="sortOrder">Position inside the group, lowest first.</param>
    /// <returns>A new active <see cref="Product"/>.</returns>
    public static Product Create(
        int groupId,
        string name,
        string? description,
        string? website,
        string? slug,
        string? packageName,
        ProductType type,
        decimal monthlyPrice,
        decimal annualPrice,
        DateTimeOffset now,
        int? serverGroupId = null,
        bool isFeatured = false,
        int sortOrder = 0)
    {
        var product = new Product
        {
            GroupId = groupId,
            Name = name,
            Description = description,
            Website = website,
            Slug = slug,
            PackageName = packageName,
            Type = type,
            Status = ProductStatus.Active,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            ServerGroupId = serverGroupId,
            IsFeatured = isFeatured,
            SortOrder = sortOrder,
        };
        product.AddDomainEvent(new ProductCreatedEvent(0, name, groupId, now));
        return product;
    }

    /// <summary>
    /// Updates the product name, description, website, prices, and server group.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="description">New description.</param>
    /// <param name="website">New website URL, or null to clear.</param>
    /// <param name="slug">New URL-friendly slug, or null to clear.</param>
    /// <param name="packageName">New hosting package name, or null to clear.</param>
    /// <param name="monthlyPrice">New monthly price.</param>
    /// <param name="annualPrice">New annual price.</param>
    /// <param name="serverGroupId">Optional FK to the server group, or null to clear.</param>
    /// <param name="isFeatured">New recommended-plan flag, or null to keep the current one.</param>
    /// <param name="sortOrder">New position inside the group, or null to keep the current one.</param>
    public void Update(
        string name,
        string? description,
        string? website,
        string? slug,
        string? packageName,
        decimal monthlyPrice,
        decimal annualPrice,
        int? serverGroupId,
        bool? isFeatured = null,
        int? sortOrder = null)
    {
        Name = name;
        Description = description;
        Website = website;
        Slug = slug;
        PackageName = packageName;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        ServerGroupId = serverGroupId;
        if (isFeatured.HasValue)
        {
            IsFeatured = isFeatured.Value;
        }

        if (sortOrder.HasValue)
        {
            SortOrder = sortOrder.Value;
        }
    }

    /// <summary>Marks the product as its group's recommended plan.</summary>
    public void Feature() => IsFeatured = true;

    /// <summary>Removes the recommended-plan mark.</summary>
    public void Unfeature() => IsFeatured = false;

    /// <summary>Moves the product to a position inside its group.</summary>
    /// <param name="sortOrder">The new position, lowest first.</param>
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>Whether <paramref name="days"/> is an acceptable grace period.</summary>
    /// <param name="days">The candidate grace period, in days.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="days"/> is from <see cref="MinGracePeriodDays"/>
    /// to <see cref="MaxGracePeriodDays"/>, inclusive.
    /// </returns>
    public static bool IsValidGracePeriod(int days)
    {
        return days is >= MinGracePeriodDays and <= MaxGracePeriodDays;
    }

    /// <summary>Whether <paramref name="metadata"/> fits in <see cref="MaxMetadataBytes"/>.</summary>
    /// <param name="metadata">The candidate metadata text, measured as sent, before it is compacted.</param>
    /// <returns>
    /// <see langword="true"/> when the text is absent or its UTF-8 form is at most
    /// <see cref="MaxMetadataBytes"/> bytes long.
    /// </returns>
    public static bool IsWithinMetadataSize(string? metadata)
    {
        return metadata is null || Encoding.UTF8.GetByteCount(metadata) <= MaxMetadataBytes;
    }

    /// <summary>Whether <paramref name="metadata"/> is well-formed JSON whose root is an object.</summary>
    /// <param name="metadata">The candidate metadata text.</param>
    /// <returns>
    /// <see langword="true"/> for a JSON object; <see langword="false"/> for an array, a scalar,
    /// <c>null</c>, empty text or text that is not JSON.
    /// </returns>
    public static bool IsValidMetadata(string? metadata)
    {
        return ParseMetadataObject(metadata) is not null;
    }

    /// <summary>
    /// Sets or clears the SKU. The value is normalised first (see <see cref="ProductSku.Normalize"/>),
    /// so <c>" lic-pro "</c> is stored as <c>"LIC-PRO"</c>.
    /// </summary>
    /// <param name="sku">The new SKU in any case, or <see langword="null"/> or white space to clear it.</param>
    /// <exception cref="DomainRuleException">
    /// <see cref="ProductsErrors.ProductSkuInvalid"/>: the normalised SKU fails <see cref="ProductSku.IsValid"/>.
    /// </exception>
    public void SetSku(string? sku)
    {
        var normalized = ProductSku.Normalize(sku);
        if (normalized is not null && !ProductSku.IsValid(normalized))
        {
            throw new DomainRuleException(
                ProductsErrors.ProductSkuInvalid,
                $"The SKU '{normalized}' must be at most {ProductSku.MaxLength} characters of letters and digits joined by single '-', '_' or '.'.");
        }

        Sku = normalized;
    }

    /// <summary>Sets or clears the metadata bag. The JSON is stored in compact form.</summary>
    /// <param name="metadata">
    /// A JSON object of at most <see cref="MaxMetadataBytes"/> bytes, or <see langword="null"/> or white
    /// space to clear the bag.
    /// </param>
    /// <exception cref="DomainRuleException">
    /// <see cref="ProductsErrors.ProductMetadataInvalid"/>: the text is larger than
    /// <see cref="MaxMetadataBytes"/> or is not a JSON object.
    /// </exception>
    public void SetMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            Metadata = null;
            return;
        }

        // Measured before parsing, so an oversized bag is refused without building its whole tree.
        if (!IsWithinMetadataSize(metadata))
        {
            throw new DomainRuleException(
                ProductsErrors.ProductMetadataInvalid,
                $"Product metadata must be at most {MaxMetadataBytes} bytes.");
        }

        var parsed = ParseMetadataObject(metadata)
            ?? throw new DomainRuleException(
                ProductsErrors.ProductMetadataInvalid,
                "Product metadata must be a JSON object.");
        Metadata = parsed.ToJsonString();
    }

    /// <summary>Sets how many days a service stays usable after its due date before it is suspended.</summary>
    /// <param name="days">The grace period, from <see cref="MinGracePeriodDays"/> to <see cref="MaxGracePeriodDays"/>.</param>
    /// <exception cref="DomainRuleException">
    /// <see cref="ProductsErrors.ProductGracePeriodOutOfRange"/>: <paramref name="days"/> fails <see cref="IsValidGracePeriod"/>.
    /// </exception>
    public void SetGracePeriodDays(int days)
    {
        if (!IsValidGracePeriod(days))
        {
            throw new DomainRuleException(
                ProductsErrors.ProductGracePeriodOutOfRange,
                $"A grace period of {days} days is outside {MinGracePeriodDays} to {MaxGracePeriodDays}.");
        }

        GracePeriodDays = days;
    }

    /// <summary>Marks the product inactive so it cannot be ordered.</summary>
    public void Deactivate() => Status = ProductStatus.Inactive;

    /// <summary>Marks the product active so it can be ordered again.</summary>
    public void Activate() => Status = ProductStatus.Active;

    /// <summary>Sets (or replaces) the price for a currency and cycle.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <param name="amount">The amount; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void SetPrice(string currencyCode, BillingCycle cycle, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A price cannot be negative.");
        }

        var code = currencyCode.ToUpperInvariant();
        var existing = _prices.FirstOrDefault(p => p.CurrencyCode == code && p.Cycle == cycle);
        if (existing is null)
        {
            _prices.Add(new ProductPrice(code, cycle, amount));
        }
        else
        {
            existing.SetAmount(amount);
        }
    }

    /// <summary>Removes every price in a currency, so the product no longer sells in it.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    public void RemovePrices(string currencyCode)
    {
        var code = currencyCode.ToUpperInvariant();
        _prices.RemoveAll(p => p.CurrencyCode == code);
    }

    /// <summary>
    /// Makes the stored prices match <paramref name="prices"/>. Every currency listed is rewritten
    /// in full: a cycle left null is removed, not kept at its old amount.
    /// </summary>
    /// <param name="prices">The prices to store, one entry per currency.</param>
    /// <param name="replaceAllCurrencies">
    /// <see langword="true"/> when a currency absent from <paramref name="prices"/> is removed, so
    /// the list is the whole truth; <see langword="false"/> when only the listed currencies change,
    /// because the caller spoke for those alone.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative.</exception>
    public void ReplacePrices(IReadOnlyCollection<ProductCurrencyPrices> prices, bool replaceAllCurrencies)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (replaceAllCurrencies)
        {
            var wanted = prices.Select(p => p.CurrencyCode.ToUpperInvariant()).ToHashSet();
            _prices.RemoveAll(p => !wanted.Contains(p.CurrencyCode));
        }

        foreach (var entry in prices)
        {
            RemovePrices(entry.CurrencyCode);
            if (entry.Monthly is { } monthly)
            {
                SetPrice(entry.CurrencyCode, BillingCycle.Monthly, monthly);
            }

            if (entry.Annual is { } annual)
            {
                SetPrice(entry.CurrencyCode, BillingCycle.Annual, annual);
            }
        }
    }

    /// <summary>Reads the stored price for a currency and cycle.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <param name="cycle">The billing cycle.</param>
    /// <returns>The amount, or <see langword="null"/> when the product has no price for that pair.</returns>
    public decimal? PriceFor(string currencyCode, BillingCycle cycle)
    {
        var code = currencyCode.ToUpperInvariant();
        return _prices.FirstOrDefault(p => p.CurrencyCode == code && p.Cycle == cycle)?.Amount;
    }

    /// <summary>Whether the product has at least one price in a currency.</summary>
    /// <param name="currencyCode">ISO 4217 alpha code, any case.</param>
    /// <returns><see langword="true"/> when a client billed in that currency can buy it.</returns>
    public bool SellsIn(string currencyCode)
    {
        var code = currencyCode.ToUpperInvariant();
        return _prices.Any(p => p.CurrencyCode == code);
    }

    /// <summary>Parses metadata text and keeps it only when its root is a JSON object.</summary>
    /// <param name="metadata">The candidate metadata text.</param>
    /// <returns>The parsed object, or <see langword="null"/> when the text is empty, not JSON, or not an object.</returns>
    private static JsonObject? ParseMetadataObject(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(metadata) as JsonObject;
        }
        catch (JsonException)
        {
            // Malformed JSON is a refusal the caller decides on, not a crash.
            return null;
        }
    }
}
