using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins <see cref="IClientCredit"/>: AddAsync and DeductAsync join the caller's unit of work, so
/// they are written only by the caller's save, a refusal stages nothing, and AddAndSaveAsync saves
/// at once and retries once on the client's row version.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An addition is saved by the caller's save and reports the new balance.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAsync_ThenSave_RaisesTheBalanceAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 5m);

        using (var scope = factory.Services.CreateScope())
        {
            var outcome = await scope.ServiceProvider.GetRequiredService<IClientCredit>()
                .AddAsync(new ClientCreditChange(clientId, 3m, CreditMovementType.AdminAdd), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);

            outcome.IsSuccess.Should().BeTrue();
            outcome.Value!.Balance.Should().Be(8m);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(8m);
    }

    /// <summary>A change whose caller never saves is not written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAsync_WithoutSave_WritesNothingAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClientCredit>()
                .AddAsync(new ClientCreditChange(clientId, 3m, CreditMovementType.AdminAdd), CancellationToken.None);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>A deduction within the balance is saved by the caller's save.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeductAsync_ThenSave_LowersTheBalanceAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 10m);

        using (var scope = factory.Services.CreateScope())
        {
            var outcome = await scope.ServiceProvider.GetRequiredService<IClientCredit>()
                .DeductAsync(new ClientCreditChange(clientId, 4m, CreditMovementType.AdminDeduct), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);

            outcome.Value!.Balance.Should().Be(6m);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(6m);
    }

    /// <summary>More than the balance is refused with <c>CLIENT_CREDIT_INSUFFICIENT</c> and stages nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeductAsync_MoreThanTheBalance_RefusesAndStagesNothingAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 2m);

        using (var scope = factory.Services.CreateScope())
        {
            var outcome = await scope.ServiceProvider.GetRequiredService<IClientCredit>()
                .DeductAsync(new ClientCreditChange(clientId, 5m, CreditMovementType.AdminDeduct), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);

            outcome.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(2m);
    }

    /// <summary>An id that names no client is refused with <c>CLIENT_NOT_FOUND</c> either way.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownClient_IsRefusedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var credit = scope.ServiceProvider.GetRequiredService<IClientCredit>();

        var added = await credit.AddAsync(new ClientCreditChange(BillingTestSeeds.NoSuchId, 1m, CreditMovementType.AdminAdd), CancellationToken.None);
        var taken = await credit.DeductAsync(new ClientCreditChange(BillingTestSeeds.NoSuchId, 1m, CreditMovementType.AdminDeduct), CancellationToken.None);

        added.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        taken.ErrorCode.Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>
    /// Two units of work both read the same balance and both deduct it in full: the first save wins,
    /// the second conflicts on the client's row version, and the balance is never overspent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeductAsync_TwoConcurrentWriters_SecondSaveConflictsAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 100m);

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstCredit = first.ServiceProvider.GetRequiredService<IClientCredit>();
        var secondCredit = second.ServiceProvider.GetRequiredService<IClientCredit>();

        // Both contexts load the client while it still holds 100.
        (await firstCredit.DeductAsync(new ClientCreditChange(clientId, 100m, CreditMovementType.AdminDeduct), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await secondCredit.DeductAsync(new ClientCreditChange(clientId, 100m, CreditMovementType.AdminDeduct), CancellationToken.None)).IsSuccess.Should().BeTrue();

        await first.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);
        var act = () => second.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>
    /// An addition saved while another writer changed the balance underneath is retried once on the
    /// fresh row, so both changes land.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddAndSaveAsync_AfterAConcurrentChange_RetriesOnTheFreshBalanceAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 10m);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // This context tracks the client at 10 before another writer moves the balance to 15.
        await context.Clients.FirstAsync(c => c.Id == clientId, CancellationToken.None);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 5m);

        var outcome = await scope.ServiceProvider.GetRequiredService<IClientCredit>()
            .AddAndSaveAsync(new ClientCreditChange(clientId, 3m, CreditMovementType.AdminAdd), CancellationToken.None);

        outcome.Value!.Balance.Should().Be(18m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(18m);
    }
}
