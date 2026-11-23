using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.OrderService;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="OrderServiceCommand"/> ordered with a free trial: the service starts as a trial and the
/// client's one trial of the product is recorded in the same save, a second trial is refused, a refused start leaves no
/// record, parallel starts give one trial, and an ordinary order records nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderServiceTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>How many rounds the parallel test runs, so a lucky interleaving does not hide a race.</summary>
    private const int ParallelRounds = 5;

    /// <summary>The trial rules every test orders with: fourteen days, converted at the end.</summary>
    private static readonly ProductTrial Trial = new(true, 14, true, [3, 1], 3);

    /// <summary>
    /// A trial order creates the service as an active trial ending the trial length from now, and the record of the
    /// client's trial of the product, linked to that service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrial_CreatesATrialServiceAndTheTrialRow()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var before = DateTimeOffset.UtcNow;

        var outcome = await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == outcome.Value, CancellationToken.None);
        service.TrialStatus.Should().Be(TrialStatus.Active);
        service.TrialEndsAt.Should().BeOnOrAfter(before.AddDays(Trial.Days));
        service.TrialEndsAt.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddDays(Trial.Days));
        var row = await context.ClientProductTrials.AsNoTracking()
            .SingleAsync(t => t.ClientId == clientId && t.ProductId == productId, CancellationToken.None);
        row.ServiceId.Should().Be(service.Id);
    }

    /// <summary>A client gets one trial of a product: a second trial order is refused and creates no second service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrialTwiceForTheSameClientAndProduct_RefusesTheSecondWithOrderTrialAlreadyUsed()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);
        var second = await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsFailure.Should().BeTrue();
        second.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, CancellationToken.None)).Should().Be(1);
        (await context.ClientProductTrials.AsNoTracking().CountAsync(t => t.ClientId == clientId, CancellationToken.None)).Should().Be(1);
    }

    /// <summary>
    /// A trial of a product whose only place is taken is refused as sold out and leaves no record, so the refused start
    /// does not use up the client's trial.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrialBlockedByThePlaceLimit_LeavesNoTrialRow()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var product = await context.Products.SingleAsync(p => p.Id == productId, CancellationToken.None);
        product.SetMaxActiveServices(1);
        var holder = Client.Create($"subject-{Guid.NewGuid():N}", "Grace", "Hopper", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(holder);
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var taken = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(holder.Id, productId, BillingCycle.Monthly, 5m, 5m, "Card"), CancellationToken.None);
        taken.IsSuccess.Should().BeTrue();

        var refused = await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
        (await context.ClientProductTrials.AsNoTracking().CountAsync(t => t.ProductId == productId, CancellationToken.None)).Should().Be(0);
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, CancellationToken.None)).Should().Be(0);
    }

    /// <summary>A trial is never ordered through an order line: the command is refused and nothing is created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrialAndAnOrderLine_IsRefusedAndCreatesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var refuse = async () => await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId) with { OrderItemId = 1 }, CancellationToken.None);

        await refuse.Should().ThrowAsync<FluentValidation.ValidationException>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, CancellationToken.None)).Should().Be(0);
        (await context.ClientProductTrials.AsNoTracking().CountAsync(t => t.ClientId == clientId, CancellationToken.None)).Should().Be(0);
    }

    /// <summary>
    /// Two trial orders of the same client and product at the same moment give one trial: one succeeds, the other is
    /// refused with <c>ORDER_TRIAL_ALREADY_USED</c>, and one service and one record exist. Run over several rounds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrialInParallel_CreatesExactlyOne()
    {
        for (var round = 0; round < ParallelRounds; round++)
        {
            int clientId;
            int productId;
            using (var seedScope = factory.Services.CreateScope())
            {
                (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(seedScope, monthlyPrice: 2.99m);
            }

            var attempts = Enumerable.Range(0, 2).Select(async _ =>
            {
                using var attemptScope = factory.Services.CreateScope();
                var bus = attemptScope.ServiceProvider.GetRequiredService<IMessageBus>();
                return await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);
            });

            var results = await Task.WhenAll(attempts);

            results.Count(r => r.IsSuccess).Should().Be(1, "round {0}", round);
            results.Single(r => r.IsFailure).ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.ClientServices.AsNoTracking()
                .CountAsync(s => s.ClientId == clientId && s.ProductId == productId, CancellationToken.None)).Should().Be(1);
            var row = await context.ClientProductTrials.AsNoTracking()
                .SingleAsync(t => t.ClientId == clientId && t.ProductId == productId, CancellationToken.None);
            row.ServiceId.Should().Be(results.Single(r => r.IsSuccess).Value);
        }
    }

    /// <summary>
    /// A second record of the same client's trial of the same product, saved through the data context, is refused as a
    /// lost race (<see cref="ConcurrencyConflictException"/>) rather than a raw database error; another product of the
    /// same client is a different trial and is saved.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SavingASecondTrialRecordForTheSameClientAndProduct_ThrowsConcurrencyConflict()
    {
        int clientId;
        int productId;
        int otherProductId;
        using (var seedScope = factory.Services.CreateScope())
        {
            (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(seedScope, monthlyPrice: 2.99m);
            (_, otherProductId) = await ServicesTestSeeds.SeedClientAndProductAsync(seedScope, monthlyPrice: 2.99m);
            var first = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            first.ClientProductTrials.Add(ClientProductTrial.Create(clientId, productId, DateTimeOffset.UtcNow));
            await first.SaveChangesAsync(CancellationToken.None);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.ClientProductTrials.Add(ClientProductTrial.Create(clientId, productId, DateTimeOffset.UtcNow));

        var act = () => context.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowExactlyAsync<ConcurrencyConflictException>();
        using var otherScope = factory.Services.CreateScope();
        var other = otherScope.ServiceProvider.GetRequiredService<AppDbContext>();
        other.ClientProductTrials.Add(ClientProductTrial.Create(clientId, otherProductId, DateTimeOffset.UtcNow));
        (await other.SaveChangesAsync(CancellationToken.None)).Should().Be(1);
    }

    /// <summary>
    /// A trial start that passes the early check but loses the unique index to a start that commits while it waits is
    /// refused with <c>ORDER_TRIAL_ALREADY_USED</c> and leaves neither a service nor a record of its own. The winning
    /// record is held uncommitted (invisible to the early check) until the order is seen waiting on it, so the lost race
    /// is certain rather than likely.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithATrialThatLosesTheUniqueIndexAfterTheCheck_RefusesWithOrderTrialAlreadyUsed()
    {
        int clientId;
        int productId;
        using (var seedScope = factory.Services.CreateScope())
        {
            (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(seedScope, monthlyPrice: 2.99m);
        }

        using var winnerScope = factory.Services.CreateScope();
        var winner = winnerScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var winnerTransaction = await winner.Database.BeginTransactionAsync(CancellationToken.None);
        winner.ClientProductTrials.Add(ClientProductTrial.Create(clientId, productId, DateTimeOffset.UtcNow));
        await winner.SaveChangesAsync(CancellationToken.None);

        var order = Task.Run(async () =>
        {
            using var orderScope = factory.Services.CreateScope();
            var bus = orderScope.ServiceProvider.GetRequiredService<IMessageBus>();
            return await bus.InvokeAsync<Result<int>>(TrialOrder(clientId, productId), CancellationToken.None);
        });

        // The order's insert of its record blocks on the uncommitted winner's row; wait until Postgres shows it waiting on
        // this transaction, so the early check has certainly passed before the winner commits.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (await CountWaitersAsync(winner) == 0)
        {
            order.IsCompleted.Should().BeFalse("the order must wait on the uncommitted record, not finish first");
            DateTime.UtcNow.Should().BeBefore(deadline, "the order never started waiting on the uncommitted record");
            await Task.Delay(20);
        }

        await winnerTransaction.CommitAsync(CancellationToken.None);
        var outcome = await order;

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId, CancellationToken.None)).Should().Be(0);
        var row = await context.ClientProductTrials.AsNoTracking()
            .SingleAsync(t => t.ClientId == clientId && t.ProductId == productId, CancellationToken.None);
        row.ServiceId.Should().BeNull();
    }

    /// <summary>An ordinary order writes no trial record and leaves the service's trial status unset.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderService_WithoutATrial_WritesNoTrialRowAndLeavesTrialStatusNull()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 2.99m);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<int>>(
            new OrderServiceCommand(clientId, productId, BillingCycle.Monthly, 5m, 5m, "Card"), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == outcome.Value, CancellationToken.None);
        service.TrialStatus.Should().BeNull();
        service.TrialEndsAt.Should().BeNull();
        (await context.ClientProductTrials.AsNoTracking().CountAsync(t => t.ClientId == clientId, CancellationToken.None)).Should().Be(0);
    }

    /// <summary>Counts the sessions waiting on a lock held by the open transaction of the given context.</summary>
    /// <param name="context">The context whose open transaction holds the lock.</param>
    /// <returns>How many lock requests wait on that transaction.</returns>
    private static async Task<int> CountWaitersAsync(AppDbContext context)
    {
        var waiters = await context.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_locks
                WHERE locktype = 'transactionid' AND NOT granted
                  AND transactionid::text = pg_current_xact_id()::text
                """)
            .ToListAsync(CancellationToken.None);
        return waiters.Single();
    }

    /// <summary>Builds a free trial order of the product for the client.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="productId">The product.</param>
    /// <returns>The command, with zero amounts and the trial rules set.</returns>
    private static OrderServiceCommand TrialOrder(int clientId, int productId) =>
        new(clientId, productId, BillingCycle.Monthly, 0m, 0m, Trial: Trial);
}
