using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Settings;

/// <summary>
/// The operator settings of email campaigns. Kept apart from <see cref="PortalSettingKeys"/> on purpose:
/// that list feeds the anonymous storefront endpoint, and none of these is public. Seeded by
/// <c>PortalSettingsSeeder</c>, edited in System Settings, validated by <c>UpdateSettingHandler</c>.
/// </summary>
public static class CampaignSettingKeys
{
    /// <summary>
    /// How many campaign mails the dispatcher sends in any one minute, over every campaign that is sending, so a large
    /// campaign cannot flood the relay or trip its own limits. The mails above the limit wait for a later pass.
    /// </summary>
    public const string SendRatePerMinute = "campaigns.send_rate_per_minute";

    /// <summary>A whole number from 1 to 600, without leading zeros: mails per minute.</summary>
    /// <remarks>Declared before <see cref="Patterns"/>: static fields initialise in textual order.</remarks>
    private static readonly ValuePattern MailsPerMinute = new(
        new Regex("^([1-9][0-9]?|[1-5][0-9]{2}|600)$", RegexOptions.CultureInvariant),
        "a whole number of mails per minute from 1 to 600",
        v => v);

    /// <summary>The shape each key accepts, with its canonical spelling.</summary>
    public static readonly IReadOnlyDictionary<string, ValuePattern> Patterns =
        new Dictionary<string, ValuePattern>
        {
            [SendRatePerMinute] = MailsPerMinute,
        };

    /// <summary>Every key with its seeded value and the description shown in System Settings.</summary>
    public static readonly IReadOnlyList<SettingDefault> Defaults =
    [
        new(SendRatePerMinute, "60", "Most campaign emails sent in one minute, over all campaigns that are sending (1 to 600). The rest wait for the next dispatch pass."),
    ];
}
