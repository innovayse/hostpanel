using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// The enrolment itself, on a clock the test moves: a failed attempt waits out its back-off unless forced, and tries again when it
/// has run out; a revoked identity is left alone unless forced; several callers at once register once; an identity that is
/// already verified costs nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RegistryInstanceEnrollmentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Runs the enrolment in a scope of a host.</summary>
    /// <param name="host">The host.</param>
    /// <param name="force">Whether to ignore the back-off.</param>
    /// <returns>The enrolment's result.</returns>
    private static async Task<Innovayse.Hostpanel.Shared.Models.Result> EnsureAsync(RegistryTestHost host, bool force)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().EnsureVerifiedAsync(force, CancellationToken.None);
    }

    /// <summary>A failed attempt is not repeated before its back-off has run out, and is repeated, and succeeds, after it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AFailedAttempt_WaitsItsBackoff_ThenTriesAgainAsync()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, clock: clock);
        var healthy = false;
        registry.Intercept = call => !healthy && call.Target == "/v1/instances"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            : null;

        var first = await EnsureAsync(host, force: false);
        var callsAfterFirst = registry.Calls.Count;
        clock.Advance(TimeSpan.FromSeconds(29));
        var tooSoon = await EnsureAsync(host, force: false);
        var callsAfterTooSoon = registry.Calls.Count;
        healthy = true;
        clock.Advance(TimeSpan.FromSeconds(2));
        var later = await EnsureAsync(host, force: false);

        first.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        tooSoon.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
        callsAfterFirst.Should().Be(1);
        callsAfterTooSoon.Should().Be(callsAfterFirst, "the pause held back the attempt 29 seconds in");
        registry.Calls.Count.Should().BeGreaterThan(callsAfterTooSoon);
        later.IsSuccess.Should().BeTrue(later.ErrorCode);
        registry.Instances.Single().Verified.Should().BeTrue();
    }

    /// <summary>The pause doubles with each failure in a row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EachFailureInARow_WaitsLongerAsync()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, clock: clock);
        registry.Intercept = call => call.Target == "/v1/instances" ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) : null;

        await EnsureAsync(host, force: false);
        clock.Advance(TimeSpan.FromSeconds(30));
        await EnsureAsync(host, force: false);

        using var scope = host.Services.CreateScope();
        var status = await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().GetStatusAsync(CancellationToken.None);
        status.State.Should().Be(RegistryInstanceState.Error);
        status.NextAttemptAt.Should().Be(clock.GetUtcNow() + TimeSpan.FromSeconds(60));
    }

    /// <summary>A forced run ignores the back-off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AForcedRun_IgnoresTheBackoffAsync()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, clock: clock);
        var healthy = false;
        registry.Intercept = call => !healthy && call.Target == "/v1/instances" ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) : null;
        (await EnsureAsync(host, force: false)).IsFailure.Should().BeTrue();
        healthy = true;

        var forced = await EnsureAsync(host, force: true);

        forced.IsSuccess.Should().BeTrue(forced.ErrorCode);
    }

    /// <summary>A revoked identity is not registered again by the automatic run, only by a forced one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARevokedIdentity_IsLeftAloneUnlessForcedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        (await EnsureAsync(host, force: false)).IsSuccess.Should().BeTrue();
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRegistryRequestSigner>()
                .ReportRefusalAsync(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED, CancellationToken.None);
        }

        var automatic = await EnsureAsync(host, force: false);
        var instancesAfterAutomatic = registry.Instances.Count;
        var forced = await EnsureAsync(host, force: true);

        automatic.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED);
        instancesAfterAutomatic.Should().Be(1);
        forced.IsSuccess.Should().BeTrue(forced.ErrorCode);
        registry.Instances.Should().HaveCount(2);
    }

    /// <summary>Several callers at once, the startup job and requests together, register the installation once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeveralCallersAtOnce_RegisterOnceAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => EnsureAsync(host, force: false)));

        results.Should().OnlyContain(r => r.IsSuccess);
        registry.Instances.Should().HaveCount(1);
        registry.Calls.Count(c => c.Target == "/v1/instances").Should().Be(1);
    }

    /// <summary>An identity the registry stops treating as verified goes back to Pending and is confirmed again by the next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnIdentityTheRegistryNoLongerTrusts_IsConfirmedAgainAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        (await EnsureAsync(host, force: false)).IsSuccess.Should().BeTrue();
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRegistryRequestSigner>()
                .ReportRefusalAsync(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED, CancellationToken.None);
            (await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().GetStatusAsync(CancellationToken.None))
                .State.Should().Be(RegistryInstanceState.Pending);
        }

        registry.Instances.Single().Verified = false;
        var again = await EnsureAsync(host, force: false);

        again.IsSuccess.Should().BeTrue(again.ErrorCode);
        registry.Instances.Should().HaveCount(1, "the same registration is verified again, it is not made anew");
        registry.Instances.Single().Verified.Should().BeTrue();
    }
}
