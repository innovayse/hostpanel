using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RetryOrderLine;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// What an order line's fulfilment does when its first attempt only half happened: a service created but not activated is
/// reused by the retry instead of ordered a second time, a registrar that times out fails only its own line and leaves it
/// retryable at once, a cancelled run releases the lines it did not finish, two retries of different lines finishing at the
/// same moment still leave the order Active, a domain that is no longer live (or that this order did not create) is not counted
/// as the registration, a run claims its lines before it acts, an outcome that cannot be recorded does not stop the other
/// lines, and a retry cancelled after its claim leaves its line retryable at once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderLineRecoveryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A digital line whose service was created but could not be activated is retried: the retry activates that same
    /// service, so the client has one service for the line (not two to provision and bill) and the line and order complete.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RetryOfADigitalLineWhoseActivationFailedReusesItsService()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        using (var failing = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(DateTimeOffset.UtcNow),
            tracker => tracker.Entries<ClientService>().Any(e => e.State == EntityState.Modified && e.Entity.Status == ServiceStatus.Active)))
        {
            var accept = await (await OrdersTestReads.AdminOfAsync(failing, factory)).PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);
            accept.EnsureSuccessStatusCode();
        }

        var digitalLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.ProductId == seeded.DigitalProductId);
        digitalLine.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        (await OrdersTestReads.CountServicesAsync(factory.Services, seeded.ClientId, seeded.DigitalProductId)).Should().Be(1);

        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{digitalLine.Id}/retry", content: null);

        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        (await OrdersTestReads.CountServicesAsync(factory.Services, seeded.ClientId, seeded.DigitalProductId)).Should().Be(1);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = await db.ClientServices.AsNoTracking().SingleAsync(s => s.ClientId == seeded.ClientId && s.ProductId == seeded.DigitalProductId);
        service.Status.Should().Be(ServiceStatus.Active);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Single(i => i.Id == digitalLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        after.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// A registrar that times out fails only the domain line, with a code: the order is still accepted, the hosting line after
    /// it is fulfilled and every outcome is on record, instead of the exception costing the whole run.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARegistrarThatTimesOutFailsOnlyItsOwnLine()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        using var host = HostWith<TimingOutRegistrar>();

        var accept = await (await OrdersTestReads.AdminOfAsync(host, factory)).PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);

        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        var domainLine = order.Items.Single(i => i.Domain == seeded.Domain);
        domainLine.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        domainLine.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        domainLine.FulfilmentAttempts.Should().Be(1);
        order.Items.Single(i => i.Domain is null).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        order.Status.Should().Be(OrderStatus.Incomplete);
        (await OrdersTestReads.CountServicesAsync(factory.Services, seeded.ClientId, seeded.ProductId)).Should().Be(1);
    }

    /// <summary>
    /// A retry whose action throws records the line failed with the code, answers with it, and leaves the line retryable
    /// at once: it is not left claimed for the length of the lease, and the next retry finishes it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARetryWhoseActionThrowsLeavesTheLineFailedAndRetryableAtOnce()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        var url = $"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry";

        using (var host = HostWith<TimingOutRegistrar>())
        {
            var thrown = await (await OrdersTestReads.AdminOfAsync(host, factory)).PostAsync(url, content: null);

            thrown.StatusCode.Should().Be(HttpStatusCode.OK);
            var failed = (await thrown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines");
            failed.GetArrayLength().Should().Be(1);
            failed[0].GetProperty("errorCode").GetString().Should().Be(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        var afterFailure = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        afterFailure.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        afterFailure.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        afterFailure.FulfilmentAttempts.Should().Be(2);

        var again = await admin.PostAsync(url, content: null);

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var done = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        done.Items.Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        done.Status.Should().Be(OrderStatus.Active);
        (await OrdersTestReads.CountDomainsAsync(factory.Services, seeded.Domain, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>
    /// A run the caller cancels part way rethrows the cancellation, but first records the line it was running and the lines
    /// after it as interrupted, so none is left unrecorded and staff can retry them at once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACancelledRunReleasesTheLinesItDidNotFinish()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        using var host = HostWith<CancellingRegistrar>();
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        using var source = new CancellationTokenSource();
        CancellingRegistrar.Source = source;

        var act = async () => await bus.InvokeAsync<Result<OrderFulfillmentDto>>(new FulfillOrderLinesCommand(seeded.OrderId), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var items = await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId);
        items.Should().OnlyContain(i =>
            i.FulfilmentState == OrderItemFulfilmentState.Failed
            && i.FulfilmentErrorCode == HostpanelErrorCodes.ORDER_LINE_INTERRUPTED
            && i.FulfilmentAttempts == 1);
        (await OrdersTestReads.CountServicesAsync(factory.Services, seeded.ClientId, seeded.ProductId)).Should().Be(0);
    }

    /// <summary>
    /// Two retries of different lines whose actions overlap, each holding the order as it loaded it before the other finished,
    /// both complete their line, and the order ends Active instead of staying Incomplete over lines that are all fulfilled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoRetriesOfDifferentLinesFinishingTogetherLeaveTheOrderActive()
    {
        var seeded = await OrdersTestSeeds.SeedTwoTakenDomainsOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var lines = await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId);
        lines.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Failed);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.FirstDomain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.SecondDomain);
        RendezvousRegistrar.Gate = new ChargeRendezvous(2);
        using var host = HostWith<RendezvousRegistrar>();
        var hostAdmin = await OrdersTestReads.AdminOfAsync(host, factory);

        var responses = await Task.WhenAll(lines.Select(l => hostAdmin.PostAsync($"/api/orders/{seeded.OrderId}/items/{l.Id}/retry", content: null)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Fulfilled && i.FulfilmentAttempts == 2);
        after.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// A domain the client holds under the line's name that is no longer live (cancelled here) is not the registration the line
    /// was for: the retry does not count it as done, it asks the registrar again, and the line stays failed with the code.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACancelledDomainTheClientHoldsIsNotCountedAsDone()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        await HoldCancelledDomainAsync(seeded.ClientId, seeded.Domain);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines").GetArrayLength().Should().Be(1);
        var after = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        after.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
    }

    /// <summary>
    /// Accepting an order claims every line before the first action runs: while the registrar is still working on the first
    /// line, the database already shows both lines in progress under their first attempt and the order Incomplete, so a crash
    /// at that moment leaves lines a retry can take over instead of lines that read as never started on an Active order.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptingAnOrderClaimsItsLinesBeforeTheFirstActionRuns()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        Order? duringAction = null;
        ProbingRegistrar.Probe = async () => duringAction = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        try
        {
            using var host = HostWith<ProbingRegistrar>();

            var accept = await (await OrdersTestReads.AdminOfAsync(host, factory)).PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);

            accept.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            ProbingRegistrar.Probe = null;
        }

        duringAction.Should().NotBeNull();
        duringAction!.Status.Should().Be(OrderStatus.Incomplete);
        duringAction.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.InProgress && i.FulfilmentAttempts == 1);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Fulfilled && i.FulfilmentAttempts == 1);
        after.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// An outcome that cannot be saved does not stop the run: the line stays claimed (retryable once its claim runs out), and
    /// the line after it is still run and recorded, instead of staying Pending on an order nothing will ever pick up.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOutcomeThatCannotBeRecordedDoesNotStopTheOtherLines()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(DateTimeOffset.UtcNow),
            tracker => tracker.Entries<OrderItem>().Any(
                e => e.State == EntityState.Modified && e.Entity.FulfilmentState == OrderItemFulfilmentState.Failed && e.Entity.Domain != null));

        var accept = await (await OrdersTestReads.AdminOfAsync(host, factory)).PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);

        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        var domainLine = order.Items.Single(i => i.Domain == seeded.Domain);
        domainLine.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        domainLine.FulfilmentAttempts.Should().Be(1);
        order.Items.Single(i => i.Domain is null).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        order.Status.Should().Be(OrderStatus.Incomplete);
        (await OrdersTestReads.CountServicesAsync(factory.Services, seeded.ClientId, seeded.ProductId)).Should().Be(1);
    }

    /// <summary>
    /// A retry the caller cancels after the line was claimed but before the run recorded anything does not leave the line in
    /// progress: it is recorded as interrupted, so the next retry takes it at once instead of waiting out the lease.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARetryCancelledAfterItsClaimLeavesTheLineInterruptedAndRetryableAtOnce()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        using var source = new CancellationTokenSource();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(new CancelAfterClaimInterceptor(source)))));
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var act = async () => await bus.InvokeAsync<Result<OrderFulfillmentDto>>(new RetryOrderLineCommand(seeded.OrderId, domainLine.Id), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var interrupted = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        interrupted.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        interrupted.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_INTERRUPTED);
        interrupted.FulfilmentAttempts.Should().Be(2);

        var again = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId)).Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// A domain the client holds under the line's name that no order created (it came from elsewhere) is not the registration
    /// the line was for: the retry does not count it as done, it asks the registrar again, and the line stays failed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALiveDomainThatNoOrderCreatedIsNotCountedAsDone()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        await HoldDomainAsync(seeded.ClientId, seeded.Domain, orderId: null);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines").GetArrayLength().Should().Be(1);
        var after = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        after.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
    }

    /// <summary>
    /// A domain a first attempt of this very order registered (stored with the order) is counted as done by the retry, so the
    /// registrar is not asked to register it a second time.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALiveDomainThisOrderCreatedIsCountedAsDone()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        await OrdersTestReads.ReleaseDomainAsync(factory.Services, seeded.Domain);
        await HoldDomainAsync(seeded.ClientId, seeded.Domain, seeded.OrderId);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines").GetArrayLength().Should().Be(0);
        (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id)
            .FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        (await OrdersTestReads.CountDomainsAsync(factory.Services, seeded.Domain, seeded.ClientId)).Should().Be(1);
    }

    /// <summary>Builds a host whose registrar is <typeparamref name="TRegistrar"/>, over the shared database.</summary>
    /// <typeparam name="TRegistrar">The registrar the host resolves the registrar port to.</typeparam>
    /// <returns>The derived host; the caller disposes it.</returns>
    private WebApplicationFactory<Program> HostWith<TRegistrar>()
        where TRegistrar : class, IRegistrarProvider =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRegistrarProvider>();
            s.AddScoped<IRegistrarProvider, TRegistrar>();
        }));

    /// <summary>Gives the client a live domain under the name.</summary>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="orderId">The order that created the domain, or <see langword="null"/> for a domain that came from elsewhere.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private async Task HoldDomainAsync(int clientId, string domain, int? orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Domains.Add(RegisteredDomain.Register(
            clientId, domain, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: "USD",
            orderId: orderId));
        await db.SaveChangesAsync();
    }

    /// <summary>Gives the client a domain under the name that was cancelled, so it is a row of theirs that is not live.</summary>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="domain">The domain name.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private async Task HoldCancelledDomainAsync(int clientId, string domain)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var held = RegisteredDomain.Register(
            clientId, domain, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: "USD");
        held.Cancel();
        db.Domains.Add(held);
        await db.SaveChangesAsync();
    }
}
