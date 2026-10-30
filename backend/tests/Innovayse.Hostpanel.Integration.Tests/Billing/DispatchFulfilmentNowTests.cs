using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="DispatchFulfilmentNowHandler"/>: the pass kicked after a payment delivers what is due, schedules
/// nothing so it never starts a second chain beside the scheduled one, and takes the scheduled chain's job
/// lease so the two never deliver at once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DispatchFulfilmentNowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The kick delivers the due record and sends no run of either dispatch command.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Kick_DeliversDueRecordsAndSchedulesNothing()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);

        var session = await scenario.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .InvokeMessageAndWaitAsync(new DispatchFulfilmentNowCommand());

        scenario.Plugin.Calls.Should().Equal([("OnInvoicePaidAsync", invoiceId)]);
        session.Sent.Envelopes()
            .Where(e => e.Message is DispatchFulfilmentCronCommand or DispatchFulfilmentNowCommand)
            .Should().BeEmpty("a kick must not start a second chain");
    }

    /// <summary>While another instance holds the dispatch job, the kick delivers nothing: the holder is already delivering.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Kick_WhileTheJobIsHeld_IsSkipped()
    {
        using var scenario = new FulfilmentScenario(factory);
        var invoiceId = await scenario.SeedInvoiceAsync();
        await scenario.SeedRecordsAsync(invoiceId, FulfilmentKind.Paid);
        using var scope = scenario.Services.CreateScope();
        var lease = scope.ServiceProvider.GetRequiredService<IJobLease>();
        var taken = await lease.TryAcquireAsync(
            new JobLeaseClaim(nameof(DispatchFulfilmentCronCommand), "another-instance", scenario.Clock.GetUtcNow(), TimeSpan.FromMinutes(5)),
            CancellationToken.None);
        taken.Acquired.Should().BeTrue();

        try
        {
            await scenario.Services.GetRequiredService<IHost>().TrackActivity()
                .Timeout(TimeSpan.FromSeconds(60))
                .InvokeMessageAndWaitAsync(new DispatchFulfilmentNowCommand());

            scenario.Plugin.Calls.Should().BeEmpty();
        }
        finally
        {
            await lease.CompleteAsync(
                new JobLeaseRelease(nameof(DispatchFulfilmentCronCommand), "another-instance", scenario.Clock.GetUtcNow(), false, TimeSpan.Zero),
                CancellationToken.None);
        }
    }
}
