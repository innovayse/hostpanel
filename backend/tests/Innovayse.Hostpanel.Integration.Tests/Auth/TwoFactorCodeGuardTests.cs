using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The per-account one-time-code guard on its own: the failure cap across attempts, the refund for a
/// correct code, and the replay check on accepted time steps. No host or database is needed.
/// </summary>
public sealed class TwoFactorCodeGuardTests
{
    /// <summary>Builds a guard with the given cap.</summary>
    /// <param name="clock">The clock the failure window is judged by.</param>
    /// <param name="maxFailures">Attempts an account may take inside the window.</param>
    /// <returns>The guard under test.</returns>
    private static TwoFactorCodeGuard Create(ManualTimeProvider clock, int maxFailures = 3)
    {
        return new TwoFactorCodeGuard(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new TwoFactorCodeGuardOptions { MaxFailures = maxFailures, WindowMinutes = 15 }),
            clock);
    }

    /// <summary>Builds a clock that only moves when a test moves it.</summary>
    /// <returns>A manual clock set to a fixed instant.</returns>
    private static ManualTimeProvider Clock()
    {
        return new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    }

    /// <summary>An account gets exactly its cap of attempts and is then locked.</summary>
    [Fact]
    public void Account_IsLockedAfterItsCap()
    {
        var guard = Create(Clock(), maxFailures: 3);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            guard.TryBeginAttempt("a").Should().BeTrue();
        }

        guard.TryBeginAttempt("a").Should().BeFalse();
    }

    /// <summary>One account being locked leaves another untouched.</summary>
    [Fact]
    public void Lock_DoesNotReachOtherAccounts()
    {
        var guard = Create(Clock(), maxFailures: 1);
        guard.TryBeginAttempt("a").Should().BeTrue();

        guard.TryBeginAttempt("a").Should().BeFalse();
        guard.TryBeginAttempt("b").Should().BeTrue();
    }

    /// <summary>A correct code gives its attempt back, so honest use never reaches the cap.</summary>
    [Fact]
    public void Forgive_GivesTheAttemptBack()
    {
        var guard = Create(Clock(), maxFailures: 1);

        for (var round = 0; round < 5; round++)
        {
            guard.TryBeginAttempt("a").Should().BeTrue();
            guard.Forgive("a");
        }
    }

    /// <summary>Concurrent attempts never exceed the cap.</summary>
    [Fact]
    public void ParallelAttempts_NeverExceedTheCap()
    {
        var guard = Create(Clock(), maxFailures: 10);
        var granted = 0;

        Parallel.For(0, 300, _ =>
        {
            if (guard.TryBeginAttempt("a"))
            {
                Interlocked.Increment(ref granted);
            }
        });

        granted.Should().Be(10);
    }

    /// <summary>A time step is accepted once; the same or an older step is a replay.</summary>
    [Fact]
    public void TimeStep_IsAcceptedOnlyOnceAndOnlyForward()
    {
        var guard = Create(Clock());

        guard.TryAcceptTimeStep("a", 100).Should().BeTrue();
        guard.TryAcceptTimeStep("a", 100).Should().BeFalse();
        guard.TryAcceptTimeStep("a", 99).Should().BeFalse();
        guard.TryAcceptTimeStep("a", 101).Should().BeTrue();
    }

    /// <summary>The replay memory is per account.</summary>
    [Fact]
    public void TimeStep_OfOneAccountDoesNotBlockAnother()
    {
        var guard = Create(Clock());
        guard.TryAcceptTimeStep("a", 100).Should().BeTrue();

        guard.TryAcceptTimeStep("b", 100).Should().BeTrue();
    }

    /// <summary>A locked account is open again once its failure window has passed.</summary>
    [Fact]
    public void Lock_EndsWhenTheWindowPasses()
    {
        var clock = Clock();
        var guard = Create(clock, maxFailures: 1);
        guard.TryBeginAttempt("a").Should().BeTrue();
        guard.TryBeginAttempt("a").Should().BeFalse();

        clock.Advance(TimeSpan.FromMinutes(15));

        guard.TryBeginAttempt("a").Should().BeTrue();
    }

    /// <summary>The window is fixed from the first failure, so a locked account stays locked until it ends.</summary>
    [Fact]
    public void Lock_HoldsInsideTheWindow()
    {
        var clock = Clock();
        var guard = Create(clock, maxFailures: 1);
        guard.TryBeginAttempt("a").Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(14));

        guard.TryBeginAttempt("a").Should().BeFalse();
    }

    /// <summary>The failure count and the replay memory survive a cache compaction under memory pressure.</summary>
    [Fact]
    public void Counters_SurviveCacheCompaction()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var guard = new TwoFactorCodeGuard(
            cache,
            Options.Create(new TwoFactorCodeGuardOptions { MaxFailures = 1, WindowMinutes = 15 }),
            Clock());
        guard.TryBeginAttempt("a").Should().BeTrue();
        guard.TryAcceptTimeStep("a", 100).Should().BeTrue();

        cache.Compact(1.0);

        guard.TryBeginAttempt("a").Should().BeFalse();
        guard.TryAcceptTimeStep("a", 100).Should().BeFalse();
    }
}
