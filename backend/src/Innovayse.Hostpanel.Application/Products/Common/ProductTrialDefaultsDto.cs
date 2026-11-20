namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>What the admin form shows for a new product's free trial, and the bounds it validates against.</summary>
/// <param name="Days">The trial length a product starts with, in days.</param>
/// <param name="AutoConvert">Whether a trial of a new product turns into a paid service at its end.</param>
/// <param name="ReminderDays">The reminder days a product starts with, descending.</param>
/// <param name="GracePeriodDays">The grace period of a converted trial a product starts with, in days.</param>
/// <param name="MinDays">The shortest trial accepted, in days.</param>
/// <param name="MaxDays">The longest trial accepted, in days.</param>
/// <param name="MinGracePeriodDays">The shortest grace period accepted, in days.</param>
/// <param name="MaxGracePeriodDays">The longest grace period accepted, in days.</param>
/// <param name="MaxReminders">The most reminder days a trial may carry.</param>
public sealed record ProductTrialDefaultsDto(
    int Days,
    bool AutoConvert,
    IReadOnlyList<int> ReminderDays,
    int GracePeriodDays,
    int MinDays,
    int MaxDays,
    int MinGracePeriodDays,
    int MaxGracePeriodDays,
    int MaxReminders);
