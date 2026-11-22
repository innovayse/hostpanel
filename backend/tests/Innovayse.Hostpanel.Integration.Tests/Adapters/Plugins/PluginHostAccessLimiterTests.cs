using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginHostAccessLimiter"/> over a real <see cref="PluginRateLimiter"/>: a budget is counted per plugin and per
/// subject, a client account (the SSH and hosting budgets) or a gateway order (the order read budget), and a client and an
/// order of the same text never share a partition. The limiter is switched on with a budget of one access a minute.
/// </summary>
public sealed class PluginHostAccessLimiterTests : IDisposable
{
    /// <summary>The plugin the keys name.</summary>
    private const string PluginId = "limiter-test";

    /// <summary>The plugin limiters, with one access a minute in each of the tiers a host access takes from.</summary>
    private readonly PluginRateLimiter _limiters = new(
        new RateLimitOptions
        {
            SshKeys = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
            HostingResources = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
            PluginOrderRead = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
        },
        disabled: false);

    /// <summary>The limiter under test.</summary>
    private readonly PluginHostAccessLimiter _limiter;

    /// <summary>Initializes the test over the limiters.</summary>
    public PluginHostAccessLimiterTests()
    {
        _limiter = new PluginHostAccessLimiter(_limiters);
    }

    /// <summary>Releases the limiters and their timers.</summary>
    public void Dispose()
    {
        _limiters.Dispose();
    }

    /// <summary>A client key names a client subject and an order key names an order subject, each under its own prefix.</summary>
    [Fact]
    public void Keys_CarryTheKindOfTheirSubject()
    {
        PluginHostAccessKey.ForClient(PluginId, 7, RateLimitPolicies.SshKeys).Subject.Should().Be("client:7");
        PluginHostAccessKey.ForOrder(PluginId, "gw-1", RateLimitPolicies.PluginOrderRead).Subject.Should().Be("order:gw-1");
    }

    /// <summary>The budget of one client is spent without touching another client's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientBudget_IsCountedPerClient()
    {
        var first = PluginHostAccessKey.ForClient(PluginId, 1, RateLimitPolicies.SshKeys);
        var other = PluginHostAccessKey.ForClient(PluginId, 2, RateLimitPolicies.SshKeys);

        (await _limiter.TryAcquireAsync(first, CancellationToken.None)).Should().BeTrue();
        (await _limiter.TryAcquireAsync(first, CancellationToken.None)).Should().BeFalse();
        (await _limiter.TryAcquireAsync(other, CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>The budget of one order is spent without touching another order's, nor another plugin's budget for the same order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderBudget_IsCountedPerPluginAndOrder()
    {
        var order = PluginHostAccessKey.ForOrder(PluginId, "gw-1", RateLimitPolicies.PluginOrderRead);

        (await _limiter.TryAcquireAsync(order, CancellationToken.None)).Should().BeTrue();
        (await _limiter.TryAcquireAsync(order, CancellationToken.None)).Should().BeFalse();
        (await _limiter.TryAcquireAsync(
            PluginHostAccessKey.ForOrder(PluginId, "gw-2", RateLimitPolicies.PluginOrderRead), CancellationToken.None)).Should().BeTrue();
        (await _limiter.TryAcquireAsync(
            PluginHostAccessKey.ForOrder("other-plugin", "gw-1", RateLimitPolicies.PluginOrderRead), CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>A client and an order whose texts are equal never share a partition, so one cannot spend the other's budget.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClientAndAnOrderOfTheSameText_DoNotShareABudget()
    {
        var client = PluginHostAccessKey.ForClient(PluginId, 7, RateLimitPolicies.HostingResources);
        var order = PluginHostAccessKey.ForOrder(PluginId, "7", RateLimitPolicies.HostingResources);

        (await _limiter.TryAcquireAsync(client, CancellationToken.None)).Should().BeTrue();
        (await _limiter.TryAcquireAsync(order, CancellationToken.None)).Should().BeTrue();
    }
}
