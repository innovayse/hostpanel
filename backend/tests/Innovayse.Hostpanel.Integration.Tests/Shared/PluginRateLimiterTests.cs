using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The limiters behind the plugin routes, on their own: a tier counts each key apart and refuses past its budget, the
/// stream cap is released when a lease is disposed, and a switched-off limiter never refuses. No host or database is involved.
/// </summary>
public sealed class PluginRateLimiterTests
{
    /// <summary>The budgets the tests use: two requests a tier, one open stream.</summary>
    private static readonly RateLimitOptions Limits = new()
    {
        PluginDefault = new RateLimitTierOptions { PermitLimit = 2, WindowSeconds = 60 },
        PluginStrict = new RateLimitTierOptions { PermitLimit = 2, WindowSeconds = 60 },
        PluginHeavy = new RateLimitTierOptions { PermitLimit = 2, WindowSeconds = 60 },
        PluginStreamPermitLimit = 1,
    };

    /// <summary>Each tier refuses past its budget for one key and leaves another key alone.</summary>
    /// <param name="policy">The tier.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(RateLimitPolicies.PluginDefault)]
    [InlineData(RateLimitPolicies.PluginStrict)]
    [InlineData(RateLimitPolicies.PluginHeavy)]
    public async Task Tier_RefusesPastItsBudgetPerKey(string policy)
    {
        using var limiter = new PluginRateLimiter(Limits, disabled: false);
        var key = PluginRateLimiter.KeyFor("billing", "client:1");

        using var first = await limiter.AcquireAsync(policy, key, CancellationToken.None);
        using var second = await limiter.AcquireAsync(policy, key, CancellationToken.None);
        using var third = await limiter.AcquireAsync(policy, key, CancellationToken.None);
        using var otherKey = await limiter.AcquireAsync(policy, PluginRateLimiter.KeyFor("billing", "client:2"), CancellationToken.None);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        third.IsAcquired.Should().BeFalse();
        otherKey.IsAcquired.Should().BeTrue();
    }

    /// <summary>A stream refused at the cap is admitted again once the lease that held it is disposed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StreamCap_IsReleasedWhenTheLeaseIsDisposed()
    {
        using var limiter = new PluginRateLimiter(Limits, disabled: false);
        var key = PluginRateLimiter.KeyFor("billing", "client:1");

        var held = await limiter.AcquireStreamAsync(key, CancellationToken.None);
        using var refused = await limiter.AcquireStreamAsync(key, CancellationToken.None);
        held.Dispose();
        using var admitted = await limiter.AcquireStreamAsync(key, CancellationToken.None);

        held.IsAcquired.Should().BeTrue();
        refused.IsAcquired.Should().BeFalse();
        admitted.IsAcquired.Should().BeTrue();
    }

    /// <summary>A switched-off limiter, as in the Testing environment, admits every request and every stream.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SwitchedOff_NeverRefuses()
    {
        using var limiter = new PluginRateLimiter(Limits, disabled: true);
        var key = PluginRateLimiter.KeyFor("billing", "client:1");

        for (var i = 0; i < 5; i++)
        {
            using var request = await limiter.AcquireAsync(RateLimitPolicies.PluginStrict, key, CancellationToken.None);
            using var stream = await limiter.AcquireStreamAsync(key, CancellationToken.None);

            request.IsAcquired.Should().BeTrue();
            stream.IsAcquired.Should().BeTrue();
        }
    }

    /// <summary>A name that is not a plugin tier is a programming error, not a silent pass.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownPolicy_Throws()
    {
        using var limiter = new PluginRateLimiter(Limits, disabled: false);

        var acquire = async () => await limiter.AcquireAsync(RateLimitPolicies.Strict, "key", CancellationToken.None);

        await acquire.Should().ThrowAsync<ArgumentException>();
    }
}
