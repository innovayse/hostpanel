namespace Innovayse.Hostpanel.Application.Products.Common;

/// <summary>The free-trial settings of a product as the API sends and accepts them.</summary>
/// <param name="Enabled">Whether the product is offered as a free trial.</param>
/// <param name="Days">How long a trial lasts, in days, from 1 to 365.</param>
/// <param name="AutoConvert">
/// Whether a trial turns into a paid service at its end (an invoice is raised); when <c>false</c> the
/// service is ended at the end of the trial.
/// </param>
/// <param name="ReminderDays">
/// The days before the end of the trial a reminder is sent on, each from 1 to <c>Days - 1</c>, without
/// repeats and at most five. Sent in any order, returned descending.
/// </param>
/// <param name="GracePeriodDays">How many days the invoice of a converted trial may stay unpaid, from 0 to 30.</param>
public sealed record ProductTrialDto(
    bool Enabled,
    int Days,
    bool AutoConvert,
    IReadOnlyList<int> ReminderDays,
    int GracePeriodDays);
