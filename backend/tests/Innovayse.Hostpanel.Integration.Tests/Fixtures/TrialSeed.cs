namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>What <see cref="TrialTestSeeds.SeedTrialAsync"/> seeds: the product's trial rules and the state of the service.</summary>
/// <param name="Days">The trial length, in days.</param>
/// <param name="AutoConvert">Whether the trial turns into a paid service at its end.</param>
/// <param name="GraceDays">How many days the conversion invoice may stay unpaid.</param>
/// <param name="ReminderDays">The reminder days, as the product stores them (descending, comma separated).</param>
/// <param name="Activate">Whether the service is activated (as a digital product); otherwise it stays Pending.</param>
/// <param name="Price">The monthly recurring price of the service.</param>
/// <param name="EndsAt">The trial end to store in place of the one the trial started with, or <see langword="null"/> to keep it.</param>
public sealed record TrialSeed(
    int Days = 14,
    bool AutoConvert = true,
    int GraceDays = 3,
    string ReminderDays = "3,1",
    bool Activate = true,
    decimal Price = 10m,
    DateTimeOffset? EndsAt = null);
