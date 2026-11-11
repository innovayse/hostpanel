namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Fills the database with the public demo's sample data and wipes it again. The one door
/// Application and the composition root use to reach the seeder, which lives in Infrastructure.
/// </summary>
public interface IDemoDataManager
{
    /// <summary>
    /// Seeds the demo accounts and sample data. Idempotent: each dataset is added only when it is
    /// absent, so a second call changes nothing. Does nothing when the demo is not enabled.
    /// </summary>
    /// <param name="ct">Cancels the seed.</param>
    /// <returns>A task that completes once the data is saved.</returns>
    public Task SeedAsync(CancellationToken ct);

    /// <summary>
    /// Wipes every business table and seeds again, inside one transaction, so a failure leaves the
    /// previous data in place. Does nothing when the demo is not enabled.
    /// </summary>
    /// <param name="ct">Cancels the reset.</param>
    /// <returns>A task that completes once the new data is committed.</returns>
    public Task ResetAsync(CancellationToken ct);
}
