using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="TerminateTrialCommand"/> against Postgres on a host whose events are dispatched: a running trial is
/// terminated and marked so, with the trial-ended mail; a trial awaiting payment loses its unpaid conversion invoice
/// first (a part-paid one is left for staff); a paid one refuses; a service whose trial is not running is refused. Every trial is seeded on a clock standing at
/// <see cref="TestMoments.Seeded"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TerminateTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The subject of the English trial-ended mail, as the seeded template words it.</summary>
    private const string EndedSubject = "has ended";

    /// <summary>A running trial is terminated, its trial marked terminated, and the client is mailed that it ended.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OfAnActiveTrial_TerminatesAndMarksTerminated()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (_, serviceId, email) = await SeedAsync(host, new TrialSeed());

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
        sender.Sent.Should().ContainSingle(m => m.Subject.Contains(EndedSubject)).Which.To.Should().Be(email);
    }

    /// <summary>
    /// Two terminations of the same trial at once (staff and the daily job, say) end it once: one call succeeds, the
    /// other is refused or loses the race on the service's concurrency token, and the client gets exactly one
    /// trial-ended mail.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_InParallel_EndsTheTrialOnceWithOneMail()
    {
        var sender = new FakeEmailSender();
        using var host = TrialTestSeeds.LiveHost(factory, sender);
        var (_, serviceId, _) = await SeedAsync(host, new TrialSeed());

        Result[] results = [];
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .DoNotAssertOnExceptionsDetected()
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async messages => results = await Task.WhenAll(
                Enumerable.Range(0, 2).Select(_ => Task.Run(() => TryTerminateAsync(host, serviceId))))));

        results.Should().HaveCount(2);
        results.Count(r => r.IsSuccess).Should().Be(1);
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
        sender.Sent.Where(m => m.Subject.Contains(EndedSubject)).Should().ContainSingle();
    }

    /// <summary>A trial awaiting payment has its unpaid conversion invoice cancelled, then is terminated.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OfAnAwaitingPaymentTrial_CancelsTheConversionInvoice()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed());
        (await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId))).IsSuccess.Should().BeTrue();

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        var invoice = (await InvoicesAsync(host, clientId)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
    }

    /// <summary>
    /// A conversion invoice Billing will not cancel because it already took part of its payment does not keep the trial
    /// running: the service is terminated and the invoice is left open for staff to settle.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_WithAPartPaidConversionInvoice_TerminatesAndLeavesTheInvoice()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed());
        (await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId))).IsSuccess.Should().BeTrue();
        await TrialTestSeeds.SettleAsync(host.Services, () => PayPartOfAsync(host, clientId));

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        (await InvoicesAsync(host, clientId)).Should().ContainSingle().Which.Status.Should().Be(InvoiceStatus.Unpaid);
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
    }

    /// <summary>
    /// A conversion invoice the client has paid (not applied yet) refuses the termination: the payment completes the
    /// conversion, so the service and the invoice are left as they are.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public Task TerminateTrial_WithAPaidConversionInvoice_IsRefused() => AssertRefusedWithInvoiceAsync(InvoiceStatus.Paid);

    /// <summary>A conversion invoice whose payment is on its way refuses the termination, leaving everything as it is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public Task TerminateTrial_WithAPaymentPendingConversionInvoice_IsRefused() => AssertRefusedWithInvoiceAsync(InvoiceStatus.PaymentPending);

    /// <summary>
    /// A conversion invoice that was paid and then partly refunded still counts as paid: the termination is refused and
    /// everything is left as it is.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public Task TerminateTrial_WithAPartiallyRefundedConversionInvoice_IsRefused() =>
        AssertRefusedWithInvoiceAsync(InvoiceStatus.PartiallyRefunded);

    /// <summary>A service whose trial was already converted to a paid one is refused, and stays as it is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OfAConvertedService_IsRefusedWithServiceNotInTrial()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (_, serviceId, _) = await SeedAsync(host, new TrialSeed());
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ClientServices.Where(s => s.Id == serviceId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.TrialStatus, TrialStatus.ConvertedToPaid), CancellationToken.None);
        }

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        (await ServiceAsync(host, serviceId)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>A trial whose service was never set up (still Pending) is terminated too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OfAPendingTrial_Terminates()
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (_, serviceId, _) = await SeedAsync(host, new TrialSeed(Activate: false));

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.IsSuccess.Should().BeTrue();
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Terminated);
        service.TrialStatus.Should().Be(TrialStatus.Terminated);
    }

    /// <summary>An unknown service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OfAnUnknownService_IsRefusedWithServiceNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new TerminateTrialCommand(ServicesTestSeeds.NoSuchServiceId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// Converts a trial, moves its conversion invoice to <paramref name="status"/> and asserts that terminating it is
    /// refused with <c>INVOICE_CANCEL_NOT_ALLOWED</c> and changes nothing.
    /// </summary>
    /// <param name="status">The invoice status that must keep the trial alive.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertRefusedWithInvoiceAsync(InvoiceStatus status)
    {
        using var host = TrialTestSeeds.LiveHost(factory, new FakeEmailSender());
        var (clientId, serviceId, _) = await SeedAsync(host, new TrialSeed());
        (await TrialTestSeeds.SettleAsync(host.Services, () => ConvertAsync(host, serviceId))).IsSuccess.Should().BeTrue();
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Invoices.Where(i => i.ClientId == clientId)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, status), CancellationToken.None);
        }

        var result = await TrialTestSeeds.SettleAsync(host.Services, () => TerminateAsync(host, serviceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_CANCEL_NOT_ALLOWED);
        (await InvoicesAsync(host, clientId)).Should().ContainSingle().Which.Status.Should().Be(status);
        var service = await ServiceAsync(host, serviceId);
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.AwaitingPayment);
    }

    /// <summary>Staff terminate-trial over HTTP answers 204; on an ordinary service 409 SERVICE_NOT_IN_TRIAL; a client token is refused 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TerminateTrial_OverHttp_MapsTheOutcome()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);
        var clientId = (await ClientsTestSeeds.RegisterClientAsync(factory, "terminate-http")).ClientId;
        var trialId = await UpgradeMyTrialTests.SeedTrialServiceAsync(factory, clientId, trial: true);
        var plainId = await UpgradeMyTrialTests.SeedTrialServiceAsync(factory, clientId, trial: false);

        var forbidden = await (await ClientsTestSeeds.RegisterClientAsync(factory, "terminate-client")).Client
            .PostAsync($"/api/services/{trialId}/terminate-trial", null);
        var plain = await admin.PostAsync($"/api/services/{plainId}/terminate-trial", null);
        var ok = await admin.PostAsync($"/api/services/{trialId}/terminate-trial", null);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        plain.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ServicesTestSeeds.CodeOfAsync(plain)).Should().Be(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == trialId);
        stored.Status.Should().Be(ServiceStatus.Terminated);
        stored.TrialStatus.Should().Be(TrialStatus.Terminated);
    }

    /// <summary>Seeds a trial on the host and waits until the seeding's own events are handled.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="seed">The trial rules and the state of the service.</param>
    /// <returns>The client id, the service id and the client's email address.</returns>
    private static Task<(int ClientId, int ServiceId, string Email)> SeedAsync(ClockedHost host, TrialSeed seed) =>
        TrialTestSeeds.SettleAsync(host.Services, () => TrialTestSeeds.SeedTrialAsync(host.Services, seed));

    /// <summary>Terminates the trial on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result.</returns>
    private static async Task<Result> TerminateAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new TerminateTrialCommand(serviceId), CancellationToken.None);
    }

    /// <summary>
    /// Terminates the trial like <see cref="TerminateAsync"/>, answering a rule or a lost race that escapes the use case
    /// as its code, so two calls at once can be compared.
    /// </summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result, or the code of the exception that stopped it.</returns>
    private static async Task<Result> TryTerminateAsync(ClockedHost host, int serviceId)
    {
        try
        {
            return await TerminateAsync(host, serviceId);
        }
        catch (DomainRuleException ex)
        {
            return Result.FailureCode(ex.Code);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.FailureCode(SharedErrorCodes.Conflict);
        }
    }

    /// <summary>Converts the trial on a fresh scope's bus, raising its conversion invoice.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="serviceId">The trial service.</param>
    /// <returns>The command's result.</returns>
    private static async Task<Result> ConvertAsync(ClockedHost host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new ConvertTrialCommand(serviceId), CancellationToken.None);
    }

    /// <summary>Records a payment of 1.00 on the client's only invoice, which leaves it open but no longer cancellable.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>Always <see langword="true"/>, once the payment is saved.</returns>
    private static async Task<bool> PayPartOfAsync(ClockedHost host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.ClientId == clientId, CancellationToken.None);
        context.Transactions.Add(invoice.AddPayment(
            TestMoments.Seeded, "manual", $"manual-{Guid.NewGuid():N}", 1m, 0m, null, TransactionSource.Manual));
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

    /// <summary>Reads the client's invoices.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The invoices.</returns>
    private static async Task<List<Invoice>> InvoicesAsync(ClockedHost host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Where(i => i.ClientId == clientId).ToListAsync(CancellationToken.None);
    }
}
