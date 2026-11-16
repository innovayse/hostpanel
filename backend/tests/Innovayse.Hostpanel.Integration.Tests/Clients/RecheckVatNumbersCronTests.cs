using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Innovayse.Hostpanel.Application.Clients.Commands.RecheckVatNumbersCron;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>RecheckVatNumbersCron</c>: the daily job that checks VAT numbers again. It picks the numbers never judged and the ones
/// whose check is older than a week, leaves fresh checks and countries without a register alone, never turns a number
/// invalid because a service was down, and always schedules its next run. The port is a fake that answers only for the numbers
/// of the test's own clients, so the other clients of the shared database are never touched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecheckVatNumbersCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A unique run of digits, so a number belongs to one test's client alone.</summary>
    /// <returns>Twelve digits.</returns>
    private static string UniqueDigits() =>
        Random.Shared.NextInt64(1000_000_000, 999_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Numbers never judged, and checks older than a week, are asked again; the verdicts are recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueNumbers_AreCheckedAndRecorded()
    {
        var never = UniqueDigits();
        var stale = UniqueDigits();
        var neverId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE" + never);
        var staleId = await VatTestSeeds.SeedClientAsync(
            factory, "FR", "FR" + stale, VatCheckOutcome.Valid, checkedAt: TestMoments.Present().AddDays(-30));
        var checker = new FakeVatNumberChecker(lookup => lookup.Number == never
            ? new VatNumberCheck(VatCheckOutcome.Valid, "Fresh Name", "Fresh Address")
            : lookup.Number == stale
                ? new VatNumberCheck(VatCheckOutcome.Invalid)
                : new VatNumberCheck(VatCheckOutcome.Unavailable));

        var session = await RunAsync(HostWith(checker));

        (await VatTestSeeds.ReadAsync(factory, neverId)).Should().Match<VatTestSeeds.StoredVat>(
            s => s.Status == VatCheckOutcome.Valid && s.Name == "Fresh Name");
        var staleNow = await VatTestSeeds.ReadAsync(factory, staleId);
        staleNow.Status.Should().Be(VatCheckOutcome.Invalid);
        staleNow.CheckedAt.Should().BeCloseTo(TestMoments.Present(), TimeSpan.FromMinutes(5));
        checker.Lookups.Select(l => l.Number).Should().Contain([never, stale]);
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>A fresh check and a client outside the countries with a register are not asked about.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FreshChecksAndCountriesWithoutARegister_AreLeftAlone()
    {
        var fresh = UniqueDigits();
        var abroad = UniqueDigits();
        var freshId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE" + fresh, VatCheckOutcome.Valid);
        await VatTestSeeds.SeedClientAsync(factory, "US", abroad);
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));

        await RunAsync(HostWith(checker));

        checker.Lookups.Select(l => l.Number).Should().NotContain([fresh, abroad]);
        (await VatTestSeeds.ReadAsync(factory, freshId)).Status.Should().Be(VatCheckOutcome.Valid);
    }

    /// <summary>
    /// A service that could not judge changes nothing: a number that was valid stays valid with the date it had, and the job
    /// still schedules its next run.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceDown_KeepsTheStatusAndStillSchedulesTheNextRun()
    {
        var number = UniqueDigits();
        var staleAt = TestMoments.Present().AddDays(-30);
        var clientId = await VatTestSeeds.SeedClientAsync(factory, "DE", "DE" + number, VatCheckOutcome.Valid, checkedAt: staleAt);
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));

        var session = await RunAsync(HostWith(checker, stopAfterUnavailable: 1000));

        var stored = await VatTestSeeds.ReadAsync(factory, clientId);
        stored.Status.Should().Be(VatCheckOutcome.Valid);
        stored.CheckedAt.Should().BeCloseTo(staleAt, TimeSpan.FromSeconds(1));
        checker.Lookups.Select(l => l.Number).Should().Contain(number);
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>After enough answers in a row with no verdict the pass stops asking and still schedules its next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceDownForAWhile_StopsThePass()
    {
        var checker = new FakeVatNumberChecker(_ => new VatNumberCheck(VatCheckOutcome.Unavailable));
        for (var i = 0; i < 3; i++)
        {
            await VatTestSeeds.SeedClientAsync(factory, "DE", "DE" + UniqueDigits());
        }

        var session = await RunAsync(HostWith(checker, stopAfterUnavailable: 2));

        checker.Lookups.Should().HaveCount(2);
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>The switch turns the outbound calls off, and the job still schedules its next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledByTheSwitch_NeverAsksTheServiceAndStillSchedulesTheNextRun()
    {
        await VatTestSeeds.SeedClientAsync(factory, "DE", "DE" + UniqueDigits());
        var checker = new FakeVatNumberChecker();

        var session = await RunAsync(HostWith(checker, disabled: true));

        checker.Lookups.Should().BeEmpty();
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>The job runs daily at 02:00 UTC unless the hour is changed, and the hour, the age and the sizes are range checked.</summary>
    [Fact]
    public void Schedule_IsDailyAtTwoUtcByDefault_AndTheSettingsAreRangeChecked()
    {
        var options = new ScheduledJobsOptions();

        options.RecheckVatNumbersHour.Should().Be(2);
        options.VatRecheckAfter.Should().Be(TimeSpan.FromDays(7));
        options.RecheckVatNumbers.NextAfter(new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero));
        options.RecheckVatNumbers.NextAfter(new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 11, 11, 2, 0, 0, TimeSpan.Zero));
        foreach (var name in new[]
        {
            nameof(ScheduledJobsOptions.RecheckVatNumbersHour),
            nameof(ScheduledJobsOptions.VatRecheckAfterDays),
            nameof(ScheduledJobsOptions.VatRecheckBatchSize),
            nameof(ScheduledJobsOptions.VatRecheckStopAfterUnavailable),
        })
        {
            typeof(ScheduledJobsOptions).GetProperty(name)!.GetCustomAttribute<RangeAttribute>().Should().NotBeNull(name);
        }
    }

    /// <summary>Builds a host whose VAT number port is the fake.</summary>
    /// <param name="checker">The fake port.</param>
    /// <param name="disabled">Whether <c>ScheduledJobs:DisableVatRechecks</c> is set.</param>
    /// <param name="stopAfterUnavailable">The value of <c>ScheduledJobs:VatRecheckStopAfterUnavailable</c>; large by default so one test's pass is not cut short by the other clients of the shared database.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWith(FakeVatNumberChecker checker, bool disabled = false, int stopAfterUnavailable = 1000) =>
        VatTestSeeds.HostWith(factory, checker).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ScheduledJobs:VatRecheckStopAfterUnavailable", stopAfterUnavailable.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("ScheduledJobs:VatRecheckBatchSize", "5000");
            if (disabled)
            {
                builder.UseSetting("ScheduledJobs:DisableVatRechecks", "true");
            }
        });

    /// <summary>Runs the job once and waits for the messages it caused.</summary>
    /// <param name="host">The host to run it in.</param>
    /// <returns>The tracked session.</returns>
    private static Task<ITrackedSession> RunAsync(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(120))
            .InvokeMessageAndWaitAsync(new RecheckVatNumbersCronCommand());

    /// <summary>Counts the runs of the job the session scheduled, apart from the one that was started by hand.</summary>
    /// <param name="session">The tracked session.</param>
    /// <returns>How many next runs were scheduled.</returns>
    private static int NextRunsOf(ITrackedSession session)
    {
        var handled = session.AllRecordsInOrder()
            .FirstOrDefault(r => r.MessageEventType == MessageEventType.ExecutionStarted
                                 && r.Envelope?.Message?.GetType() == typeof(RecheckVatNumbersCronCommand))
            ?.Envelope?.Id;

        return session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == typeof(RecheckVatNumbersCronCommand) && e.Id != handled)
            .Select(e => e.Id)
            .Distinct()
            .Count();
    }
}
