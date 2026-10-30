using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="DispatchFulfilmentCronHandler"/>: the scheduled chain delivers what is due and schedules its own
/// next run exactly once. What a pass delivers, and how, is pinned in <see cref="FulfilmentDispatcherTests"/>;
/// that a failing run still schedules the next one is pinned for every job in
/// <c>ScheduledJobsSurviveFailureTests</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DispatchFulfilmentCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The cron run delivers the due record and leaves exactly one next run scheduled.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Run_DeliversDueRecordsAndSchedulesTheNextRunOnce()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);

        // Ten days ahead, so the run the handler schedules is never delivered while the test runs.
        scenario.Clock.Advance(TimeSpan.FromDays(10));
        var session = await scenario.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new DispatchFulfilmentCronCommand());

        scenario.Plugin.Calls.Should().Equal([("OnInvoicePaidAsync", invoiceId)]);
        (await scenario.ReadAsync(invoiceId, FulfilmentKind.Paid)).Status.Should().Be(FulfilmentStatus.Done);
        var handled = session.AllRecordsInOrder()
            .First(r => r.MessageEventType == MessageEventType.ExecutionStarted
                        && r.Envelope?.Message?.GetType() == typeof(DispatchFulfilmentCronCommand))
            .Envelope!.Id;
        session.Sent.Envelopes()
            .Where(e => e.Message?.GetType() == typeof(DispatchFulfilmentCronCommand) && e.Id != handled)
            .Select(e => e.Id)
            .Distinct()
            .Should().HaveCount(1);
    }
}
