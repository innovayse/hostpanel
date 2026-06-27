namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Configuration and pricing for a single TLD extension.
/// Stores both cost (registrar) and sell (client-facing) prices per registration period,
/// along with administrative metadata such as enabled state and sort order.
/// </summary>
public sealed class TldConfig : Entity
{
    /// <summary>Gets the TLD extension without the leading dot (e.g. "am", "com").</summary>
    public string Tld { get; private set; } = string.Empty;

    /// <summary>Gets the registrar module responsible for this TLD.</summary>
    public RegistrarModule RegistrarModule { get; private set; }

    /// <summary>Gets the ISO 4217 currency code for cost prices (registrar currency).</summary>
    public string Currency { get; private set; } = "AMD";

    /// <summary>Gets the ISO 4217 currency code for sell prices (client-facing currency).</summary>
    public string SellCurrency { get; private set; } = "USD";

    /// <summary>Gets whether this TLD is enabled for sale to clients.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Gets the display sort order (ascending).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Backing field for TLD categories.</summary>
    private List<string> _categories = [];

    /// <summary>Gets the marketing categories this TLD belongs to (e.g. "popular", "country").</summary>
    public IReadOnlyList<string> Categories => _categories.AsReadOnly();

    /// <summary>Backing field for cost registration prices.</summary>
    private Dictionary<int, decimal> _costRegister = [];

    /// <summary>Gets the registrar cost for registration, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> CostRegister => _costRegister;

    /// <summary>Backing field for cost transfer prices.</summary>
    private Dictionary<int, decimal> _costTransfer = [];

    /// <summary>Gets the registrar cost for transfer, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> CostTransfer => _costTransfer;

    /// <summary>Backing field for cost renewal prices.</summary>
    private Dictionary<int, decimal> _costRenew = [];

    /// <summary>Gets the registrar cost for renewal, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> CostRenew => _costRenew;

    /// <summary>Backing field for sell registration prices.</summary>
    private Dictionary<int, decimal> _sellRegister = [];

    /// <summary>Gets the client-facing sell price for registration, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> SellRegister => _sellRegister;

    /// <summary>Backing field for sell transfer prices.</summary>
    private Dictionary<int, decimal> _sellTransfer = [];

    /// <summary>Gets the client-facing sell price for transfer, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> SellTransfer => _sellTransfer;

    /// <summary>Backing field for sell renewal prices.</summary>
    private Dictionary<int, decimal> _sellRenew = [];

    /// <summary>Gets the client-facing sell price for renewal, keyed by period in years.</summary>
    public IReadOnlyDictionary<int, decimal> SellRenew => _sellRenew;

    /// <summary>Gets the UTC timestamp of the last successful price sync from the registrar, or <see langword="null"/> if never synced.</summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when this configuration was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private TldConfig() : base(0) { }

    /// <summary>
    /// Creates a new TLD configuration with the specified settings.
    /// </summary>
    /// <param name="tld">TLD extension without the leading dot (e.g. "am", "com").</param>
    /// <param name="registrarModule">The registrar module responsible for this TLD.</param>
    /// <param name="currency">ISO 4217 currency code for cost prices.</param>
    /// <param name="sellCurrency">ISO 4217 currency code for sell prices.</param>
    /// <param name="isEnabled">Whether this TLD is enabled for sale.</param>
    /// <param name="sortOrder">Display sort order (ascending).</param>
    /// <param name="categories">Marketing categories for this TLD.</param>
    /// <returns>A new <see cref="TldConfig"/> instance.</returns>
    public static TldConfig Create(
        string tld, RegistrarModule registrarModule, string currency, string sellCurrency,
        bool isEnabled, int sortOrder, List<string> categories)
    {
        return new TldConfig
        {
            Tld = tld.Trim().ToLowerInvariant(),
            RegistrarModule = registrarModule,
            Currency = currency,
            SellCurrency = sellCurrency,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
            _categories = [.. categories],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Updates the admin-configurable fields of this TLD configuration.
    /// </summary>
    /// <param name="registrarModule">The registrar module responsible for this TLD.</param>
    /// <param name="currency">ISO 4217 currency code for cost prices.</param>
    /// <param name="sellCurrency">ISO 4217 currency code for sell prices.</param>
    /// <param name="isEnabled">Whether this TLD is enabled for sale.</param>
    /// <param name="sortOrder">Display sort order (ascending).</param>
    /// <param name="categories">Marketing categories for this TLD.</param>
    public void Update(
        RegistrarModule registrarModule, string currency, string sellCurrency,
        bool isEnabled, int sortOrder, List<string> categories)
    {
        RegistrarModule = registrarModule;
        Currency = currency;
        SellCurrency = sellCurrency;
        IsEnabled = isEnabled;
        SortOrder = sortOrder;
        _categories = [.. categories];
    }

    /// <summary>
    /// Replaces the client-facing sell prices for registration, transfer, and renewal.
    /// Creates new dictionary instances so EF Core change tracking detects modifications on jsonb columns.
    /// </summary>
    /// <param name="register">Sell registration prices keyed by period in years.</param>
    /// <param name="transfer">Sell transfer prices keyed by period in years.</param>
    /// <param name="renew">Sell renewal prices keyed by period in years.</param>
    public void UpdateSellPrices(
        Dictionary<int, decimal> register, Dictionary<int, decimal> transfer, Dictionary<int, decimal> renew)
    {
        _sellRegister = new Dictionary<int, decimal>(register);
        _sellTransfer = new Dictionary<int, decimal>(transfer);
        _sellRenew = new Dictionary<int, decimal>(renew);
    }

    /// <summary>
    /// Replaces the registrar cost prices for registration, transfer, and renewal.
    /// Creates new dictionary instances so EF Core change tracking detects modifications on jsonb columns.
    /// </summary>
    /// <param name="register">Cost registration prices keyed by period in years.</param>
    /// <param name="transfer">Cost transfer prices keyed by period in years.</param>
    /// <param name="renew">Cost renewal prices keyed by period in years.</param>
    public void UpdateCostPrices(
        Dictionary<int, decimal> register, Dictionary<int, decimal> transfer, Dictionary<int, decimal> renew)
    {
        _costRegister = new Dictionary<int, decimal>(register);
        _costTransfer = new Dictionary<int, decimal>(transfer);
        _costRenew = new Dictionary<int, decimal>(renew);
    }

    /// <summary>
    /// Marks this TLD configuration as having been successfully synced with the registrar at the current time.
    /// </summary>
    public void MarkSynced()
    {
        LastSyncedAt = DateTimeOffset.UtcNow;
    }
}
