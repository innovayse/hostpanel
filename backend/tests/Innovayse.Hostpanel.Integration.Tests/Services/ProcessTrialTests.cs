using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CancelService;
using Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="ProcessTrialCommand"/> against Postgres on a host whose events are dispatched and whose mail goes to a
/// fake relay: a running trial gets each reminder once, an ended trial is converted or terminated, a trial awaiting
/// payment is terminated when its grace ends unpaid or part paid (but kept when its invoice is paid), a trial that is
/// to end at the end of its period is terminated without an invoice, and an ordinary service is left alone. Every
/// trial is seeded on a clock standing at <see cref="TestMoments.Seeded"/> with reminder days 3 and 1 and a grace of
/// three days; a test moves the clock to run the step "days later".
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProcessTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The subject of the English trial-ending mail, as the seeded template words it.</summary>
    private const string EndingSubject = "ends in";

    /// <summary>The subject of the English trial-converted mail, as the seeded template words it.</summary>
    private const string ConvertedSubject = "is now a paid service";

    /// <summary>The subject of the English trial-ended mail, as the seeded template words it.</summary>
    private const string EndedSubject = "has ended";

    /// <summary>
    /// Three days before the end the three-day reminder is recorded and mailed; a second run the same day sends nothing.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_ThreeDaysLeft_SendsTheReminderOnce()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (_, serviceId, email) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(3)));

        var first = await ProcessAsync(host, serviceId);
        var second = await ProcessAsync(host, serviceId);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        (await RemindersAsync(host, serviceId)).Should().Equal(3);
        var mail = sender.Sent.Should().ContainSingle(m => m.Subject.Contains(EndingSubject)).Subject;
        mail.To.Should().Be(email);
        mail.Subject.Should().Contain("3");
    }

    /// <summary>
    /// The day after the three-day reminder (two days left) the due reminder is still the three-day one, already sent,
    /// so nothing more is sent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_TwoDaysLeftAfterTheThreeDayReminder_SendsNothing()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (_, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(3)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();

        host.Clock.Set(TestMoments.Seeded.AddDays(1));
        var result = await ProcessAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        (await RemindersAsync(host, serviceId)).Should().Equal(3);
        sender.Sent.Where(m => m.Subject.Contains(EndingSubject)).Should().ContainSingle();
    }

    /// <summary>
    /// An ended trial that converts by itself awaits payment, gets one unpaid conversion invoice for the period from
    /// its end, and the client is mailed that it became a paid service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_EndedWithAutoConvert_RaisesTheInvoiceAndAwaitsPayment()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var endsAt = TestMoments.Seeded.AddDays(-1);
        var (clientId, serviceId, email) = await SeedAsync(host, new TrialSeed(EndsAt: endsAt));

        var result = await ProcessAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        service.NextRenewalAt.Should().Be(endsAt);
        service.TrialPaymentDueAt.Should().Be(endsAt.AddDays(3));
        var invoice = (await InvoicesAsync(host, clientId)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.Items.Should().ContainSingle().Which.RenewalPeriodStart.Should().Be(endsAt);
        sender.Sent.Should().ContainSingle(m => m.Subject.Contains(ConvertedSubject)).Which.To.Should().Be(email);
    }

    /// <summary>An ended trial of a product that does not convert is terminated, with the trial-ended mail and no invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_EndedWithoutAutoConvert_TerminatesWithTrialEnded()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, email) = await SeedAsync(
            host, new TrialSeed(AutoConvert: false, EndsAt: TestMoments.Seeded.AddDays(-1)));

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial ended");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().BeEmpty();
        sender.Sent.Should().ContainSingle(m => m.Subject.Contains(EndedSubject)).Which.To.Should().Be(email);
        sender.Sent.Should().NotContain(m => m.Subject.Contains(ConvertedSubject));
    }

    /// <summary>
    /// A trial that ends while its service was never set up (Pending, no renewal date) is terminated even though the
    /// product converts: nothing ran, so nothing is billed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_EndedPendingTrial_IsTerminatedEvenWithAutoConvert()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(
            host, new TrialSeed(Activate: false, EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ServiceAsync(host, serviceId)).NextRenewalAt.Should().BeNull();

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial ended");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().BeEmpty();
    }

    /// <summary>An ended trial whose automation terminates it at the end of the cycle is terminated, never converted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_EndedTrialWithEndOfCycleTermination_TerminatesWithoutAnInvoice()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        await TrialTestSeeds.SettleAsync(host.Services, () => SetEndOfCycleTerminationAsync(host, serviceId));

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial cancelled");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().BeEmpty();
        sender.Sent.Should().NotContain(m => m.Subject.Contains(ConvertedSubject));
    }

    /// <summary>An ended trial with an open end-of-period cancellation request is terminated, never converted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_EndedTrialWithAnOpenCancellationRequest_TerminatesWithoutAnInvoice()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        await TrialTestSeeds.SettleAsync(host.Services, () => RequestEndOfPeriodCancellationAsync(host, serviceId));

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial cancelled");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().BeEmpty();
        sender.Sent.Should().NotContain(m => m.Subject.Contains(ConvertedSubject));
    }

    /// <summary>
    /// A converted trial whose invoice is still unpaid when the grace ends has the invoice cancelled and is terminated.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_AwaitingPaymentPastDueAndUnpaid_CancelsTheInvoiceAndTerminates()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();

        host.Clock.Set(TestMoments.Seeded.AddDays(3));
        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial not paid");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Cancelled);
        sender.Sent.Should().ContainSingle(m => m.Subject.Contains(EndedSubject));
    }

    /// <summary>
    /// The daily run the day after a trial was terminated for not paying finds nothing to do: the trial ends once, its
    /// invoice is cancelled once and the client gets exactly one trial-ended mail.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_RunAgainAfterTheTermination_EndsTheTrialOnceWithOneMail()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        host.Clock.Set(TestMoments.Seeded.AddDays(3));
        var (first, firstReasons) = await ProcessTrackedAsync(host, serviceId);

        host.Clock.Set(TestMoments.Seeded.AddDays(4));
        var (second, secondReasons) = await ProcessTrackedAsync(host, serviceId);

        first.IsSuccess.Should().BeTrue();
        firstReasons.Should().Equal("Free trial not paid");
        second.IsSuccess.Should().BeTrue();
        secondReasons.Should().BeEmpty();
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Cancelled);
        sender.Sent.Where(m => m.Subject.Contains(EndedSubject)).Should().ContainSingle();
    }

    /// <summary>
    /// A client who paid the conversion invoice just before the grace ended, with the payment not applied to the service
    /// yet, keeps the service: it is not terminated and the invoice stays paid.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_GraceEndedWithThePaidInvoice_DoesNotTerminate()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        await MarkInvoicesPaidAsync(host, clientId);

        host.Clock.Set(TestMoments.Seeded.AddDays(3));
        var result = await ProcessAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Paid);
        sender.Sent.Should().NotContain(m => m.Subject.Contains(EndedSubject));
    }

    /// <summary>
    /// A conversion invoice paid and then partly refunded counts as paid: the trial past its grace is not terminated.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_GraceEndedWithAPartiallyRefundedInvoice_DoesNotTerminate()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        await MarkInvoicesAsync(host, clientId, InvoiceStatus.PartiallyRefunded);

        host.Clock.Set(TestMoments.Seeded.AddDays(3));
        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().BeEmpty();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        sender.Sent.Should().NotContain(m => m.Subject.Contains(EndedSubject));
    }

    /// <summary>
    /// A conversion invoice that took only part of its payment when the grace ends cannot be cancelled, yet the trial is
    /// terminated: the invoice is left open for staff to settle instead of keeping the service running unpaid.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_GraceEndedWithAPartPaidInvoice_TerminatesAndLeavesTheInvoice()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        await PayPartOfAsync(host, clientId);

        host.Clock.Set(TestMoments.Seeded.AddDays(3));
        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial not paid");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Unpaid);
        sender.Sent.Should().ContainSingle(m => m.Subject.Contains(EndedSubject));
    }

    /// <summary>
    /// A trial awaiting payment that the client cancels at the end of the period is terminated on the next run, before
    /// its due date, and its unpaid invoice is cancelled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_AwaitingPaymentWithACancellation_TerminatesAtOnce()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        await TrialTestSeeds.SettleAsync(host.Services, () => RequestEndOfPeriodCancellationAsync(host, serviceId));

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().Equal("Free trial cancelled");
        await AssertTerminatedAsync(host, serviceId);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>
    /// The job running twice on a reminder day sends one reminder, and running twice on the end day raises one
    /// conversion invoice and one trial-converted mail.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_RunTwice_SendsOneReminderAndRaisesOneInvoice()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var endsAt = TestMoments.Seeded.AddDays(3);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: endsAt));

        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        host.Clock.Set(endsAt);
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();

        sender.Sent.Where(m => m.Subject.Contains(EndingSubject)).Should().ContainSingle();
        (await RemindersAsync(host, serviceId)).Should().Equal(3);
        (await ServiceAsync(host, serviceId)).TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle();
        sender.Sent.Where(m => m.Subject.Contains(ConvertedSubject)).Should().ContainSingle();
    }

    /// <summary>
    /// A trial awaiting payment whose client asked to cancel at the period end but whose conversion invoice is already
    /// paid (the payment not applied yet) is left alone: the payment completes the conversion.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_AwaitingPaymentWithACancellationAndAPaidInvoice_DoesNothing()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        (await ProcessAsync(host, serviceId)).IsSuccess.Should().BeTrue();
        await MarkInvoicesPaidAsync(host, clientId);
        await TrialTestSeeds.SettleAsync(host.Services, () => RequestEndOfPeriodCancellationAsync(host, serviceId));

        var (result, reasons) = await ProcessTrackedAsync(host, serviceId);

        result.IsSuccess.Should().BeTrue();
        reasons.Should().BeEmpty();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        (await InvoicesAsync(host, clientId, includeCancelled: true)).Should().ContainSingle()
            .Which.Status.Should().Be(InvoiceStatus.Paid);
        sender.Sent.Should().NotContain(m => m.Subject.Contains(EndedSubject));
    }

    /// <summary>
    /// Two runs at once on a reminder day both answer success and record and mail the reminder once: the loser's insert
    /// hits the unique index and is answered as already sent. The handler chain is built first by a run on an unknown
    /// service, so the parallel calls reach the database together.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_InParallel_SendsOneReminder()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (_, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(3)));
        (await ProcessAsync(host, ServicesTestSeeds.NoSuchServiceId)).IsSuccess.Should().BeTrue();

        var results = await TrialTestSeeds.SettleAsync(
            host.Services, () => Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => InvokeAsync(host, serviceId)))));

        results.Should().HaveCount(2).And.OnlyContain(r => r.IsSuccess);
        (await RemindersAsync(host, serviceId)).Should().Equal(3);
        sender.Sent.Where(m => m.Subject.Contains(EndingSubject)).Should().ContainSingle();
    }

    /// <summary>A service that never was a trial is a success with nothing done.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ProcessTrial_OrdinaryService_DoesNothing()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, TestMoments.Seeded.AddDays(-5));
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new ProcessTrialCommand(seeded.ServiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == seeded.ServiceId, CancellationToken.None);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().BeNull();
        (await context.TrialReminders.AnyAsync(r => r.ServiceId == seeded.ServiceId, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>Seeds a trial on the host and waits until the seeding's own events are handled.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="seed">The trial rules and the state of the service.</param>
    /// <returns>The client id, the service id and the client's email address.</returns>
    private static Task<(int ClientId, int ServiceId, string Email)> SeedAsync(ClockedHost host, TrialSeed seed) =>
        TrialTestSeeds.SettleAsync(host.Services, () => TrialTestSeeds.SeedTrialAsync(host.Services, seed));

    /// <summary>Runs the trial's daily step on a fresh scope's bus and waits until the mail it set off is sent.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result.</returns>
    private static Task<Result> ProcessAsync(ClockedHost host, int serviceId) =>
        TrialTestSeeds.SettleAsync(host.Services, () => InvokeAsync(host, serviceId));

    /// <summary>
    /// Runs the trial's daily step like <see cref="ProcessAsync"/> and also reads the reasons of the trial terminations
    /// it dispatched, so a test can tell which row of the decision table terminated the trial.
    /// </summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result and the reasons of the <see cref="TerminateTrialCommand"/>s it dispatched.</returns>
    private static async Task<(Result Result, List<string?> Reasons)> ProcessTrackedAsync(ClockedHost host, int serviceId)
    {
        Result result = null!;
        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ => result = await InvokeAsync(host, serviceId)));
        var reasons = session.Executed.MessagesOf<TerminateTrialCommand>().Select(c => c.Reason).ToList();
        return (result, reasons);
    }

    /// <summary>Runs the trial's daily step on a fresh scope's bus, without waiting for what it sets off.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result.</returns>
    private static async Task<Result> InvokeAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new ProcessTrialCommand(serviceId), CancellationToken.None);
    }

    /// <summary>Files an end-of-period cancellation request for the service.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>Always <see langword="true"/>, once the request is filed.</returns>
    private static async Task<bool> RequestEndOfPeriodCancellationAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var cancelled = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, "Not needed"), CancellationToken.None);
        cancelled.IsSuccess.Should().BeTrue();
        return true;
    }

    /// <summary>Sets the automation's end-of-cycle termination on the service.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>Always <see langword="true"/>, once the change is saved.</returns>
    private static async Task<bool> SetEndOfCycleTerminationAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        service.ConfigureAutomation(service.AutoRenew, service.OverrideAutoSuspend, service.SuspendUntil, autoTerminateEndOfCycle: true, null);
        await context.SaveChangesAsync(CancellationToken.None);
        return true;
    }

    /// <summary>Marks the client's invoices paid in the database, standing for a payment not applied to the service yet.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>A task that completes once the invoices are updated.</returns>
    private static Task MarkInvoicesPaidAsync(ClockedHost host, int clientId) => MarkInvoicesAsync(host, clientId, InvoiceStatus.Paid);

    /// <summary>Sets the status of the client's invoices in the database, standing for a payment not applied to the service yet.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="status">The status to set.</param>
    /// <returns>A task that completes once the invoices are updated.</returns>
    private static async Task MarkInvoicesAsync(ClockedHost host, int clientId, InvoiceStatus status)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.Invoices.Where(i => i.ClientId == clientId)
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, status), CancellationToken.None);
    }

    /// <summary>Records a payment of 1.00 on the client's only invoice, which leaves it open but no longer cancellable.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>A task that completes once the payment is saved.</returns>
    private static async Task PayPartOfAsync(ClockedHost host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.ClientId == clientId, CancellationToken.None);
        context.Transactions.Add(invoice.AddPayment(
            TestMoments.Seeded, "manual", $"manual-{Guid.NewGuid():N}", 1m, 0m, null, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Asserts the service is terminated and its trial marked terminated.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertTerminatedAsync(ClockedHost host, int serviceId)
    {
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
    }

    /// <summary>Reads the service as stored.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The id of the service to read.</param>
    /// <returns>The service as stored.</returns>
    private static async Task<ClientService> ServiceAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId, CancellationToken.None);
    }

    /// <summary>Reads the reminder days recorded for the service.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The reminder days, largest first.</returns>
    private static async Task<List<int>> RemindersAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.TrialReminders.AsNoTracking().Where(r => r.ServiceId == serviceId)
            .OrderByDescending(r => r.DaysBefore).Select(r => r.DaysBefore).ToListAsync(CancellationToken.None);
    }

    /// <summary>Reads the client's invoices with their lines.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="includeCancelled">Whether cancelled invoices are read too.</param>
    /// <returns>The invoices.</returns>
    private static async Task<List<Invoice>> InvoicesAsync(ClockedHost host, int clientId, bool includeCancelled = false)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Items)
            .Where(i => i.ClientId == clientId && (includeCancelled || i.Status != InvoiceStatus.Cancelled))
            .ToListAsync(CancellationToken.None);
    }
}
