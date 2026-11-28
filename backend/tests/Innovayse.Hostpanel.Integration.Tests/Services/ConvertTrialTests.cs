using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CancelService;
using Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="ConvertTrialCommand"/> against Postgres on a host whose events are dispatched: a running trial is saved
/// awaiting payment and gets one conversion invoice due after the grace, with the trial-converted mail; a trial that
/// cannot be converted is refused with nothing raised. Every trial is seeded with a grace of three days, on a clock
/// standing at <see cref="TestMoments.Seeded"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConvertTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The subject of the English trial-converted mail, as the seeded template words it.</summary>
    private const string ConvertedSubject = "is now a paid service";

    /// <summary>
    /// An early upgrade of a running trial starts the paid period now, is due the grace days later, raises one invoice
    /// for that period and mails the client that the trial became a paid service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OfARunningTrial_RaisesTheInvoiceAndAwaitsPayment()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, email) = await SeedAsync(host, new TrialSeed());

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        var now = TestMoments.Seeded;
        var service = await ServiceAsync(host, serviceId);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        service.NextRenewalAt.Should().Be(now);
        service.TrialEndsAt.Should().Be(now);
        service.TrialPaymentDueAt.Should().Be(now.AddDays(3));
        var invoice = (await InvoicesAsync(host, clientId)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DueDate.Should().Be(now.AddDays(3));
        var line = invoice.Items.Should().ContainSingle().Subject;
        line.ClientServiceId.Should().Be(serviceId);
        line.RenewalPeriodStart.Should().Be(now);
        var mail = sender.Sent.Should().ContainSingle(m => m.Subject.Contains(ConvertedSubject)).Subject;
        mail.To.Should().Be(email);
        mail.Body.Should().Contain(now.AddDays(3).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A trial converted after its end starts the paid period at the trial end, and is due the grace days after it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_AfterTheEnd_StartsAtTheTrialEnd()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var endsAt = TestMoments.Seeded.AddDays(-1);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: endsAt));

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        var service = await ServiceAsync(host, serviceId);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        service.NextRenewalAt.Should().Be(endsAt);
        service.TrialPaymentDueAt.Should().Be(endsAt.AddDays(3));
        var invoice = (await InvoicesAsync(host, clientId)).Should().ContainSingle().Subject;
        invoice.DueDate.Should().Be(endsAt.AddDays(3));
        invoice.Items.Should().ContainSingle().Which.RenewalPeriodStart.Should().Be(endsAt);
    }

    /// <summary>A trial whose service was never set up (still Pending) is refused, and nothing is invoiced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OfAPendingTrial_IsRefusedWithServiceNotActive()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(Activate: false));

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        (await ServiceAsync(host, serviceId)).TrialStatus.Should().Be(TrialStatus.Active);
        (await InvoicesAsync(host, clientId)).Should().BeEmpty();
    }

    /// <summary>A service that never was a trial is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OfAnOrdinaryService_IsRefusedWithServiceNotInTrial()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, TestMoments.Seeded.AddDays(5));
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new ConvertTrialCommand(seeded.ServiceId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
    }

    /// <summary>An unknown service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OfAnUnknownService_IsRefusedWithServiceNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new ConvertTrialCommand(ServicesTestSeeds.NoSuchServiceId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// A trial already awaiting payment whose invoice was never raised (Billing refused it, or the call died) gets it
    /// on a second convert, which is an idempotent success.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OfAnAwaitingPaymentTrialWithoutInvoice_RaisesIt()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed(EndsAt: TestMoments.Seeded.AddDays(-1)));
        var (start, dueAt) = await TrialTestSeeds.SettleAsync(
            host.Services, () => TrialTestSeeds.BeginConversionAsync(host.Services, serviceId, TestMoments.Seeded));

        var first = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));
        var second = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var invoice = (await InvoicesAsync(host, clientId)).Should().ContainSingle().Subject;
        invoice.DueDate.Should().Be(dueAt);
        invoice.Items.Should().ContainSingle().Which.RenewalPeriodStart.Should().Be(start);
        (await ServiceAsync(host, serviceId)).TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
    }

    /// <summary>
    /// Converts of one trial at once (staff racing the daily job and the client's upgrade) all answer success, with no
    /// exception escaping the lost saves, the service ends awaiting payment, and one invoice is raised. The handler
    /// chain is built first by a convert of an unknown service, so the parallel calls reach the database together.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_InParallel_RaisesOneInvoice()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed());
        (await ConvertAsync(host, ServicesTestSeeds.NoSuchServiceId)).ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);

        var results = await TrialTestSeeds.SettleAsync(
            host.Services, () => Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => ConvertAsync(host, serviceId)))));

        results.Should().OnlyContain(r => r.IsSuccess);
        (await ServiceAsync(host, serviceId)).TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
        (await InvoicesAsync(host, clientId)).Should().ContainSingle();

        // The losers' conversion events die with their lost saves, so the client is told once.
        sender.Sent.Where(m => m.Subject.Contains(ConvertedSubject)).Should().ContainSingle();
    }

    /// <summary>
    /// A trial that is to end at the end of its period (an end-of-period cancellation request, or the automation's
    /// end-of-cycle termination) is never converted: it is refused before anything changes, with no invoice, and the
    /// daily job terminates it instead.
    /// </summary>
    /// <param name="how">How the end was asked for: <c>request</c> or <c>automation</c>.</param>
    /// <param name="awaitingPayment">Whether the trial was already awaiting payment.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("request", false)]
    [InlineData("automation", false)]
    [InlineData("request", true)]
    public async Task ConvertTrial_OfATrialEndingAtThePeriodEnd_IsRefused(string how, bool awaitingPayment)
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed());
        if (awaitingPayment)
        {
            await TrialTestSeeds.SettleAsync(host.Services, () => TrialTestSeeds.BeginConversionAsync(host.Services, serviceId, TestMoments.Seeded));
        }

        await TrialTestSeeds.SettleAsync(host.Services, () => EndAtPeriodEndAsync(host, serviceId, how));
        var expected = awaitingPayment ? TrialStatus.AwaitingPayment : TrialStatus.Active;

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
        (await ServiceAsync(host, serviceId)).TrialStatus.Should().Be(expected);
        (await InvoicesAsync(host, clientId)).Should().BeEmpty();
    }

    /// <summary>Staff convert-trial over HTTP answers 204; on an ordinary service 409 SERVICE_NOT_IN_TRIAL; a client token is refused 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertTrial_OverHttp_MapsTheOutcome()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var clientId = (await ClientsTestSeeds.RegisterClientAsync(factory, "convert-http")).ClientId;
        var trialId = await UpgradeMyTrialTests.SeedTrialServiceAsync(factory, clientId, trial: true);
        var plainId = await UpgradeMyTrialTests.SeedTrialServiceAsync(factory, clientId, trial: false);

        var forbidden = await (await ClientsTestSeeds.RegisterClientAsync(factory, "convert-client")).Client
            .PostAsync($"/api/services/{trialId}/convert-trial", null);
        var plain = await admin.PostAsync($"/api/services/{plainId}/convert-trial", null);
        var ok = await admin.PostAsync($"/api/services/{trialId}/convert-trial", null);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        plain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(plain)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == trialId);
        stored.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
    }

    /// <summary>Seeds a trial on the host and waits until the seeding's own events are handled.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="seed">The trial rules and the state of the service.</param>
    /// <returns>The client id, the service id and the client's email address.</returns>
    private static Task<(int ClientId, int ServiceId, string Email)> SeedAsync(ClockedHost host, TrialSeed seed) =>
        TrialTestSeeds.SettleAsync(host.Services, () => TrialTestSeeds.SeedTrialAsync(host.Services, seed));

    /// <summary>Converts the trial on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result.</returns>
    private static async Task<Result> ConvertAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new ConvertTrialCommand(serviceId), CancellationToken.None);
    }

    /// <summary>Asks for the service to end at the end of its period.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <param name="how"><c>request</c> files an end-of-period cancellation; <c>automation</c> only sets the end-of-cycle termination.</param>
    /// <returns>Always <see langword="true"/>, once the change is saved.</returns>
    private static async Task<bool> EndAtPeriodEndAsync(ClockedHost host, int serviceId, string how)
    {
        using var scope = host.Services.CreateScope();
        if (how == "request")
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var cancelled = await bus.InvokeAsync<Result>(
                new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, "Not needed"), CancellationToken.None);
            cancelled.IsSuccess.Should().BeTrue();
            return true;
        }

        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        service.ConfigureAutomation(service.AutoRenew, service.OverrideAutoSuspend, service.SuspendUntil, autoTerminateEndOfCycle: true, null);
        await context.SaveChangesAsync(CancellationToken.None);
        return true;
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

    /// <summary>Reads the client's invoices that are not cancelled, with their lines.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The invoices.</returns>
    private static async Task<List<Invoice>> InvoicesAsync(ClockedHost host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Items)
            .Where(i => i.ClientId == clientId && i.Status != InvoiceStatus.Cancelled)
            .ToListAsync(CancellationToken.None);
    }
}
