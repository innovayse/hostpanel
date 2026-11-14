namespace Innovayse.Hostpanel.Domain.Products;

/// <summary>
/// The free-trial settings of a product: whether a client may start a trial of it without paying, how
/// long it lasts, whether it turns into a paid service at its end, when reminders go out, and how long
/// the first invoice may stay unpaid after a conversion.
/// </summary>
/// <param name="Enabled">Whether the product is offered as a free trial.</param>
/// <param name="Days">How long a trial lasts, in days, from <see cref="MinDays"/> to <see cref="MaxDays"/>.</param>
/// <param name="AutoConvert">
/// Whether the trial turns into a paid service at its end (an invoice is raised); when <c>false</c> the
/// service is ended at the end of the trial.
/// </param>
/// <param name="ReminderDays">
/// The days before the end of the trial a reminder is sent on, each from 1 to <c>Days - 1</c>, without
/// repeats and at most <see cref="MaxReminders"/> of them. Always read in descending order.
/// </param>
/// <param name="GracePeriodDays">
/// How many days the invoice of a converted trial may stay unpaid before the service is ended, from
/// <see cref="MinGracePeriodDays"/> to <see cref="MaxGracePeriodDays"/>.
/// </param>
public sealed record ProductTrial(
    bool Enabled,
    int Days,
    bool AutoConvert,
    IReadOnlyList<int> ReminderDays,
    int GracePeriodDays)
{
    /// <summary>The shortest trial accepted, in days.</summary>
    public const int MinDays = 1;

    /// <summary>The longest trial accepted, in days.</summary>
    public const int MaxDays = 365;

    /// <summary>The trial length a product starts with, in days.</summary>
    public const int DefaultDays = 14;

    /// <summary>The shortest grace period of a converted trial accepted, in days: none at all.</summary>
    public const int MinGracePeriodDays = 0;

    /// <summary>The longest grace period of a converted trial accepted, in days.</summary>
    public const int MaxGracePeriodDays = 30;

    /// <summary>The grace period of a converted trial a product starts with, in days.</summary>
    public const int DefaultGracePeriodDays = 3;

    /// <summary>The most reminder days a trial may carry.</summary>
    public const int MaxReminders = 5;

    /// <summary>The reminder days a product starts with, as stored.</summary>
    public const string DefaultReminderDaysText = "3,1";

    /// <summary>Gets the settings of a product that has none: the trial is off and the rest are the defaults.</summary>
    public static ProductTrial Default { get; } = new(false, DefaultDays, true, ParseReminderDays(DefaultReminderDaysText), DefaultGracePeriodDays);

    /// <summary>Tells whether the length, reminder days and grace period are all inside what is accepted.</summary>
    /// <param name="days">The trial length, in days.</param>
    /// <param name="reminderDays">The reminder days.</param>
    /// <param name="gracePeriodDays">The grace period of a converted trial, in days.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="days"/> is from <see cref="MinDays"/> to <see cref="MaxDays"/>,
    /// <paramref name="gracePeriodDays"/> from <see cref="MinGracePeriodDays"/> to <see cref="MaxGracePeriodDays"/>,
    /// and the reminder days are at most <see cref="MaxReminders"/>, each from 1 to <paramref name="days"/> minus 1,
    /// without repeats.
    /// </returns>
    public static bool IsValid(int days, IReadOnlyCollection<int>? reminderDays, int gracePeriodDays)
    {
        if (days is < MinDays or > MaxDays || gracePeriodDays is < MinGracePeriodDays or > MaxGracePeriodDays)
        {
            return false;
        }

        if (reminderDays is null || reminderDays.Count > MaxReminders)
        {
            return false;
        }

        return reminderDays.All(day => day >= 1 && day < days) && reminderDays.Distinct().Count() == reminderDays.Count;
    }

    /// <summary>Writes reminder days the way they are stored: descending, separated by commas.</summary>
    /// <param name="reminderDays">The reminder days.</param>
    /// <returns>The text, empty for none.</returns>
    public static string FormatReminderDays(IEnumerable<int> reminderDays)
    {
        ArgumentNullException.ThrowIfNull(reminderDays);

        return string.Join(',', reminderDays.OrderByDescending(day => day));
    }

    /// <summary>Reads reminder days from their stored text.</summary>
    /// <param name="text">The stored text: comma separated whole numbers, or empty for none.</param>
    /// <returns>The days, descending.</returns>
    public static IReadOnlyList<int> ParseReminderDays(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture))
            .OrderByDescending(day => day)
            .ToList();
    }
}
