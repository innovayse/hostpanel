using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The operator settings of fraud detection. Kept apart from <see cref="PortalSettingKeys"/> on purpose: that list feeds the
/// anonymous storefront endpoint, and none of these is public. Seeded by <c>PortalSettingsSeeder</c>, read through
/// <c>SettingSource</c> and written by the fraud settings use case, which also holds the rule that the review threshold lies
/// below the reject threshold.
/// </summary>
public static class FraudSettingKeys
{
    /// <summary>Whether registrations and orders are checked at all. Off until the operator turns it on.</summary>
    public const string Enabled = "fraud.enabled";

    /// <summary>The risk score from which an order is held for staff review.</summary>
    public const string ManualReviewRiskScore = "fraud.manual_review_risk_score";

    /// <summary>The risk score from which a request is refused without review.</summary>
    public const string AutoRejectRiskScore = "fraud.auto_reject_risk_score";

    /// <summary>The length of the window, in minutes, that orders from one IP address are counted over.</summary>
    public const string VelocityWindowMinutes = "fraud.velocity_window_minutes";

    /// <summary>How many orders one IP address may place within the window before the next one scores risk.</summary>
    public const string VelocityMaxOrders = "fraud.velocity_max_orders";

    /// <summary>How many accounts one IP address may carry before the next one scores risk.</summary>
    public const string MaxAccountsPerIp = "fraud.max_accounts_per_ip";

    /// <summary>Every key, in the order the settings are shown.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Enabled, ManualReviewRiskScore, AutoRejectRiskScore, VelocityWindowMinutes, VelocityMaxOrders, MaxAccountsPerIp,
    ];

    /// <summary>The switch: <c>true</c> or <c>false</c> in any case, stored in lower case.</summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern Switch = new(
        new Regex("^(true|false)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        "true or false",
        v => v.ToLowerInvariant());

    /// <summary>A risk score: a whole number from 0 to 100, without leading zeros.</summary>
    private static readonly ValuePattern Score = new(
        new Regex("^(100|[1-9]?[0-9])$", RegexOptions.CultureInvariant),
        "a whole number from 0 to 100",
        v => v);

    /// <summary>A window in minutes: a whole number from 1 to 1440 (a day), without leading zeros.</summary>
    private static readonly ValuePattern Minutes = new(
        new Regex("^([1-9][0-9]{0,2}|1[0-3][0-9]{2}|14[0-3][0-9]|1440)$", RegexOptions.CultureInvariant),
        "a whole number of minutes from 1 to 1440",
        v => v);

    /// <summary>A count: a whole number from 1 to 100, without leading zeros.</summary>
    private static readonly ValuePattern Count = new(
        new Regex("^([1-9][0-9]?|100)$", RegexOptions.CultureInvariant),
        "a whole number from 1 to 100",
        v => v);

    /// <summary>The shape each key accepts, with its canonical spelling.</summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [Enabled] = Switch,
            [ManualReviewRiskScore] = Score,
            [AutoRejectRiskScore] = Score,
            [VelocityWindowMinutes] = Minutes,
            [VelocityMaxOrders] = Count,
            [MaxAccountsPerIp] = Count,
        };

    /// <summary>Every key with its seeded value and the description shown in System Settings.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(Enabled, "false", "Check registrations and orders for fraud (true/false). Block lists and the built-in signals work without any external service."),
        new(ManualReviewRiskScore, "50", "Risk score (0 to 100) from which an order is held for staff review. Must be lower than the auto-reject score; edit both together in Fraud settings."),
        new(AutoRejectRiskScore, "90", "Risk score (0 to 100) from which a registration or order is refused without review. Must be higher than the review score; edit both together in Fraud settings."),
        new(VelocityWindowMinutes, "60", "Minutes over which orders from one IP address are counted (1 to 1440)."),
        new(VelocityMaxOrders, "3", "Most orders one IP address may place within that window before the next one scores risk (1 to 100)."),
        new(MaxAccountsPerIp, "3", "Most accounts one IP address may carry before the next one scores risk (1 to 100)."),
    ];
}
