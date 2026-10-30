using System.ComponentModel.DataAnnotations;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// <see cref="ScheduledJobsOptions"/> validation of the migration heartbeat: a job is abandoned only
/// after it missed several beats, never at the heartbeat interval itself.
/// </summary>
public sealed class ScheduledJobsOptionsTests
{
    /// <summary>The defaults leave room for many heartbeats before a job counts as abandoned.</summary>
    [Fact]
    public void Defaults_AreValid()
    {
        Validate(new ScheduledJobsOptions()).Should().BeEmpty();
    }

    /// <summary>A stale threshold at or under two heartbeats is refused, so a slow save never fails a healthy pull.</summary>
    /// <param name="heartbeatSeconds">The heartbeat interval.</param>
    /// <param name="staleMinutes">The stale threshold.</param>
    [Theory]
    [InlineData(60, 2)]
    [InlineData(60, 1)]
    [InlineData(300, 5)]
    public void StaleThresholdWithinTwoHeartbeats_IsRefused(int heartbeatSeconds, int staleMinutes)
    {
        var options = new ScheduledJobsOptions { MigrationHeartbeatSeconds = heartbeatSeconds, MigrationStaleAfterMinutes = staleMinutes };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>The wait between two asks about one pending gateway session doubles with every ask and stops at the upper bound.</summary>
    /// <param name="checks">How many times the session was already asked about.</param>
    /// <param name="expectedMinutes">The wait before the next ask.</param>
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(7, 320)]
    [InlineData(8, 360)]
    [InlineData(50, 360)]
    public void GatewayCheckInterval_DoublesUpToTheUpperBound(int checks, int expectedMinutes)
    {
        new ScheduledJobsOptions().GatewayCheckInterval(checks).Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    /// <summary>An upper bound shorter than the back-off start is refused.</summary>
    [Fact]
    public void GatewayCheckMaxBelowItsStart_IsRefused()
    {
        var options = new ScheduledJobsOptions { GatewayCheckBackoffStartMinutes = 30, GatewayCheckMaxIntervalMinutes = 10 };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>The fulfilment dispatch runs every 30 seconds, its claim lasts twice the call timeout, and the defaults fit the job lease.</summary>
    [Fact]
    public void FulfilmentDispatch_DefaultsAreThirtySecondsWithATwiceTimeoutLease()
    {
        var options = new ScheduledJobsOptions();

        options.DispatchFulfilmentIntervalSeconds.Should().Be(30);
        options.FulfilmentLease.Should().Be(options.FulfilmentCallTimeout * 2);
        (options.FulfilmentCallTimeout * options.FulfilmentBatchSize).Should().BeLessThan(options.Lease);
    }

    /// <summary>A batch that could outlast the job lease is refused, or another instance would start a second pass over the same records.</summary>
    [Fact]
    public void FulfilmentBatchThatCouldOutlastTheJobLease_IsRefused()
    {
        var options = new ScheduledJobsOptions { FulfilmentBatchSize = 100, FulfilmentCallTimeoutSeconds = 60, LeaseMinutes = 60 };

        Validate(options).Should().ContainSingle();
    }

    /// <summary>Runs the full options validation, annotations and relations alike.</summary>
    /// <param name="options">The options to check.</param>
    /// <returns>The failures found.</returns>
    private static List<ValidationResult> Validate(ScheduledJobsOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
