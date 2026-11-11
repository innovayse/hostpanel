using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRatesCron;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// Integration tests for the <c>UpdateExchangeRatesCron</c> use case: the daily job that refreshes the exchange rates
/// the way the admin panel's button does. A refusal of the update never stops the job, the next run is always scheduled,
/// and a switch turns the outbound call off.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateExchangeRatesCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The rate the fake source quotes for AMD.</summary>
    private const decimal QuotedAmdRate = 0.0021000m;

    /// <summary>A run writes what the source quotes and schedules the next run once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SuccessfulRun_WritesTheQuotedRatesAndSchedulesTheNextRunOnceAsync()
    {
        var source = new FakeExchangeRateSource(new Dictionary<string, decimal> { ["AMD"] = QuotedAmdRate });
        var host = HostWith(source);

        var session = await RunAsync(host);

        (await RateOfAsync("AMD")).Should().Be(QuotedAmdRate);
        source.RequestedBase.Should().NotBeNullOrEmpty();
        source.RequestedCodes.Should().NotContain(source.RequestedBase!).And.Contain("AMD");
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>A currency the source does not quote keeps the rate it had.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACurrencyTheSourceDoesNotQuote_KeepsItsRateAsync()
    {
        var source = new FakeExchangeRateSource(new Dictionary<string, decimal> { ["AMD"] = QuotedAmdRate });
        var host = HostWith(source);
        await PutCurrencyAsync(host, "SEK", rateToBase: 0.1m);

        await RunAsync(host);

        (await RateOfAsync("SEK")).Should().Be(0.1m, "a currency the source does not name is never zeroed");
        (await RateOfAsync("AMD")).Should().Be(QuotedAmdRate);
    }

    /// <summary>A source that is down leaves every rate as it was, throws nothing and still schedules the next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SourceDown_ChangesNothingAndStillSchedulesTheNextRunAsync()
    {
        var before = await RateOfAsync("AMD");
        var source = new FakeExchangeRateSource(
            new Dictionary<string, decimal> { ["AMD"] = 9m }, HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
        var host = HostWith(source);

        var session = await RunAsync(host);

        (await RateOfAsync("AMD")).Should().Be(before);
        NextRunsOf(session).Should().Be(1, "the next run is scheduled once even when the source was down");
    }

    /// <summary>With no base currency configured the run does nothing and still schedules the next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoBaseCurrency_SkipsTheRunAndStillSchedulesTheNextRunAsync()
    {
        var source = new FakeExchangeRateSource(new Dictionary<string, decimal> { ["AMD"] = QuotedAmdRate });
        var host = HostWith(source);
        var baseCode = await SetBaseAsync(null);
        try
        {
            var session = await RunAsync(host);

            source.RequestedBase.Should().BeNull("with no base there is nothing to ask the source");
            NextRunsOf(session).Should().Be(1);
        }
        finally
        {
            await SetBaseAsync(baseCode);
        }
    }

    /// <summary>The switch turns the outbound call off, and the job still schedules its next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledByTheSwitch_NeverAsksTheSourceAndStillSchedulesTheNextRunAsync()
    {
        var before = await RateOfAsync("AMD");
        var source = new FakeExchangeRateSource(new Dictionary<string, decimal> { ["AMD"] = 9m });
        var host = HostWith(source, disabled: true);

        var session = await RunAsync(host);

        source.RequestedBase.Should().BeNull("the source is never asked while the job is switched off");
        (await RateOfAsync("AMD")).Should().Be(before);
        NextRunsOf(session).Should().Be(1);
    }

    /// <summary>The job runs daily at 03:00 UTC unless the hour is changed, and the hour must be a real one.</summary>
    [Fact]
    public void Schedule_IsDailyAtThreeUtcByDefault_AndTheHourIsRangeChecked()
    {
        var options = new ScheduledJobsOptions();
        var range = typeof(ScheduledJobsOptions).GetProperty(nameof(ScheduledJobsOptions.UpdateExchangeRatesHour))!
            .GetCustomAttribute<RangeAttribute>();

        options.UpdateExchangeRatesHour.Should().Be(3);
        options.UpdateExchangeRates.NextAfter(new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero));
        options.UpdateExchangeRates.NextAfter(new DateTimeOffset(2026, 11, 10, 4, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTimeOffset(2026, 11, 11, 3, 0, 0, TimeSpan.Zero));
        range.Should().NotBeNull();
        ((int)range!.Minimum, (int)range.Maximum).Should().Be((0, 23));
    }

    /// <summary>Builds a host whose rate source is the fake, optionally with the update switched off.</summary>
    /// <param name="source">The fake source.</param>
    /// <param name="disabled">Whether <c>ScheduledJobs:DisableExchangeRateUpdates</c> is set.</param>
    /// <returns>The host.</returns>
    private WebApplicationFactory<Program> HostWith(FakeExchangeRateSource source, bool disabled = false) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (disabled)
            {
                builder.UseSetting("ScheduledJobs:DisableExchangeRateUpdates", "true");
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IExchangeRateSource>(source));
        });

    /// <summary>Runs the job once and waits for the messages it caused.</summary>
    /// <param name="host">The host to run it in.</param>
    /// <returns>The tracked session.</returns>
    private static Task<ITrackedSession> RunAsync(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new UpdateExchangeRatesCronCommand());

    /// <summary>Creates or replaces a currency through the admin API.</summary>
    /// <param name="host">The host to call.</param>
    /// <param name="code">The currency code.</param>
    /// <param name="rateToBase">Its rate to the base currency.</param>
    /// <returns>A task that completes when it is saved.</returns>
    private async Task PutCurrencyAsync(WebApplicationFactory<Program> host, string code, decimal rateToBase)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var response = await client.PutAsJsonAsync($"/api/admin/currencies/{code}", new
        {
            numeric = "752",
            prefix = string.Empty,
            suffix = " kr",
            decimals = 2,
            rateToBase,
            isEnabled = true,
        });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Reads the stored rate of a currency.</summary>
    /// <param name="code">The currency code.</param>
    /// <returns>The rate to the base currency.</returns>
    private async Task<decimal> RateOfAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Currencies.AsNoTracking().Where(c => c.Code == code).Select(c => c.RateToBase).SingleAsync();
    }

    /// <summary>Makes the given currency the only base one, or none when the code is <see langword="null"/>.</summary>
    /// <param name="code">The code of the new base currency, or <see langword="null"/> for no base.</param>
    /// <returns>The code of the currency that was the base before.</returns>
    private async Task<string?> SetBaseAsync(string? code)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var previous = await context.Currencies.AsNoTracking().Where(c => c.IsBase).Select(c => c.Code).FirstOrDefaultAsync();
        await context.Currencies.Where(c => c.IsBase).ExecuteUpdateAsync(set => set.SetProperty(c => c.IsBase, false));
        if (code is not null)
        {
            await context.Currencies.Where(c => c.Code == code).ExecuteUpdateAsync(set => set.SetProperty(c => c.IsBase, true));
        }

        return previous;
    }

    /// <summary>Counts the runs of the job the session scheduled, apart from the one that was started by hand.</summary>
    /// <param name="session">The tracked session.</param>
    /// <returns>How many next runs were scheduled.</returns>
    private static int NextRunsOf(ITrackedSession session)
    {
        var handled = session.AllRecordsInOrder()
            .FirstOrDefault(r => r.MessageEventType == MessageEventType.ExecutionStarted
                                 && r.Envelope?.Message?.GetType() == typeof(UpdateExchangeRatesCronCommand))
            ?.Envelope?.Id;

        return session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == typeof(UpdateExchangeRatesCronCommand) && e.Id != handled)
            .Select(e => e.Id)
            .Distinct()
            .Count();
    }
}
