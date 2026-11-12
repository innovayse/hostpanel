using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The billing engine's operator settings. Kept apart from <see cref="PortalSettingKeys"/>
/// on purpose: that list feeds the anonymous storefront endpoint, and none of these is public.
/// Seeded by <c>PortalSettingsSeeder</c>, edited in System Settings, validated by <c>UpdateSettingHandler</c>.
/// </summary>
public static class BillingSettingKeys
{
    /// <summary>Days before the renewal date a renewal invoice is raised.</summary>
    public const string InvoiceDaysBefore = "billing.renewal.invoice_days_before";

    /// <summary>Comma-separated days before due on which a reminder is mailed; empty turns reminders off.</summary>
    public const string ReminderDays = "billing.renewal.reminder_days";

    /// <summary>Whether a client's credit balance pays renewal invoices automatically.</summary>
    public const string AutoApplyCredit = "billing.renewal.auto_apply_credit";

    /// <summary>Days an invoice may stay overdue before the services on it are suspended.</summary>
    public const string SuspendAfterOverdueDays = "billing.renewal.suspend_after_overdue_days";

    /// <summary>Days a service suspended for non-payment may stay suspended before it is terminated automatically.</summary>
    public const string TerminateAfterSuspendedDays = "billing.renewal.terminate_after_suspended_days";

    /// <summary>The least a client may add to their credit balance in one top-up, in the client's billing currency.</summary>
    public const string CreditTopUpMinimum = "billing.credit.topup_min";

    /// <summary>The most a client may add to their credit balance in one top-up, in the client's billing currency.</summary>
    public const string CreditTopUpMaximum = "billing.credit.topup_max";

    /// <summary>The most unpaid credit top-up invoices a client may hold at once, so top-ups cannot pile up without being paid.</summary>
    public const string CreditTopUpMaxOpen = "billing.credit.topup_max_open";

    /// <summary>Whether a client's downgrade is approved at once; when off, staff approve it and the credit is paid out then.</summary>
    public const string AutoApproveDowngrades = "billing.plan_change.auto_approve_downgrades";

    /// <summary>Whether tax is added to new invoices from the tax rules; when off, no invoice is taxed automatically.</summary>
    public const string TaxEnabled = "billing.tax.enabled";

    /// <summary>
    /// The largest day count any of these settings accepts (the day-count and reminder patterns
    /// stop at 90). A reader that must look as far ahead as any setting can reach uses this instead
    /// of its own literal; the renewal cron does not, it reads the configured lead time.
    /// </summary>
    public const int MaxDays = 90;

    /// <summary>
    /// A day count of at least one, up to 90: the renewal lead time and the delay before a suspended
    /// service is terminated. At least one day, because a zero lead time would raise the invoice on the
    /// renewal date itself, due that same day, leaving the client no time to pay before the period starts,
    /// and a zero delay would terminate a service in the same run that found it suspended.
    /// </summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern LeadDays = new(
        new Regex("^([1-9]|[1-8][0-9]|90)$", RegexOptions.CultureInvariant),
        "a whole number of days from 1 to 90",
        v => v);

    /// <summary>A day count from 0 to 90, for settings where zero means "on the day".</summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern DayCount = new(
        new Regex("^([0-9]|[1-8][0-9]|90)$", RegexOptions.CultureInvariant),
        "a whole number of days from 0 to 90",
        v => v);

    /// <summary>
    /// A money amount above zero with at most two decimals and at most nine whole digits: the top-up limits. It is stored as
    /// typed, because a decimal amount has no other spelling a reader would not parse the same.
    /// </summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern TopUpAmount = new(
        new Regex("^(?!0+(\\.0{1,2})?$)[0-9]{1,9}(\\.[0-9]{1,2})?$", RegexOptions.CultureInvariant),
        "an amount above zero with at most two decimals",
        v => v);

    /// <summary>A whole number from 1 to 20: how many unpaid top-up invoices one client may hold.</summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern OpenCount = new(
        new Regex("^([1-9]|1[0-9]|20)$", RegexOptions.CultureInvariant),
        "a whole number from 1 to 20",
        v => v);

    /// <summary>The switches: <c>true</c> or <c>false</c> in any case, stored in lower case.</summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern TrueOrFalse = new(
        new Regex("^(true|false)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        "true or false",
        v => v.ToLowerInvariant());

    /// <summary>The shape each key accepts, with its canonical spelling.</summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [InvoiceDaysBefore] = LeadDays,
            [SuspendAfterOverdueDays] = DayCount,
            [TerminateAfterSuspendedDays] = LeadDays,
            [ReminderDays] = new(
                new Regex("^\\s*([1-9]|[1-8][0-9]|90)\\s*(,\\s*([1-9]|[1-8][0-9]|90)\\s*)*$", RegexOptions.CultureInvariant),
                "comma-separated days from 1 to 90, like 7,3,1",
                v => string.Join(',', v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))),
            [CreditTopUpMinimum] = TopUpAmount,
            [CreditTopUpMaximum] = TopUpAmount,
            [CreditTopUpMaxOpen] = OpenCount,
            [AutoApplyCredit] = TrueOrFalse,
            [AutoApproveDowngrades] = TrueOrFalse,
            [TaxEnabled] = TrueOrFalse,
        };

    /// <summary>Every key with its seeded value and the description shown in System Settings.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(InvoiceDaysBefore, "14", "Days before a service or domain renews that its renewal invoice is raised."),
        new(ReminderDays, "7,3,1", "Days before a renewal invoice is due on which a reminder is mailed. Empty sends none."),
        new(AutoApplyCredit, "true", "Pay renewal invoices from the client's credit balance automatically (true/false)."),
        new(SuspendAfterOverdueDays, "3", "Days an invoice may stay overdue before the services on it are suspended."),
        new(TerminateAfterSuspendedDays, "14", "Days a service suspended for non-payment stays suspended before it is terminated and its hosting account removed."),
        new(CreditTopUpMinimum, "5", "Least a client can add to their credit balance in one top-up, in the client's billing currency."),
        new(CreditTopUpMaximum, "1000", "Most a client can add to their credit balance in one top-up, in the client's billing currency."),
        new(CreditTopUpMaxOpen, "3", "Most unpaid top-up invoices a client can hold at once; they pay or cancel one before raising another."),
        new(AutoApproveDowngrades, "false", "Approve a client's downgrade to a cheaper plan at once and credit the rest of the billing cycle to their balance (true/false). When off, staff approve each downgrade."),
        new(TaxEnabled, "true", "Add tax to new invoices from the tax rules of the client's country (true/false). When off, no invoice is taxed automatically."),
    ];

    /// <summary>Keys that may be saved empty although they seed with a value.</summary>
    private static readonly IReadOnlySet<string> Clearable = new HashSet<string>(StringComparer.Ordinal) { ReminderDays };

    /// <summary>Whether <paramref name="key"/> is a billing key that may be saved empty.</summary>
    /// <param name="key">The setting key.</param>
    /// <returns><see langword="true"/> for a clearable billing key.</returns>
    public static bool AllowsEmpty(string key) => Clearable.Contains(key);
}
