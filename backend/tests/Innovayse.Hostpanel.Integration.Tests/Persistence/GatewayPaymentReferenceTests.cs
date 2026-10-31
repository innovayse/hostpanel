using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The filtered unique index on the payment method and reference of gateway payment ledger rows,
/// against Postgres: a gateway payment is recorded once, while manual entries, imports and gateway
/// refunds may repeat a reference.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentReferenceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A second gateway payment row under the same method and reference is refused, as the lost race it is.</summary>
    [Fact]
    public async Task SecondGatewayPaymentWithTheSameReference_IsRefusedAsAConflict()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();
        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayPayment);

        var act = () => AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayPayment);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        (await CountAsync(reference)).Should().Be(1);
    }

    /// <summary>Manual rows with one reference are all kept: the reference is typed by a person, not promised by a gateway.</summary>
    [Fact]
    public async Task ManualRowsWithTheSameReference_AreAllowed()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();

        await AddAsync(clientId, "Manual", reference, TransactionSource.Manual);
        await AddAsync(clientId, "Manual", reference, TransactionSource.Manual);

        (await CountAsync(reference)).Should().Be(2);
    }

    /// <summary>Imported rows with one reference are all kept: the source system owns its references.</summary>
    [Fact]
    public async Task ImportedRowsWithTheSameReference_AreAllowed()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();

        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.Import);
        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.Import);

        (await CountAsync(reference)).Should().Be(2);
    }

    /// <summary>A manual row and a gateway payment may share a reference, so recording by hand never blocks a gateway.</summary>
    [Fact]
    public async Task ManualRowAndGatewayPaymentWithTheSameReference_AreAllowed()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();

        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.Manual);
        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayPayment);

        (await CountAsync(reference)).Should().Be(2);
    }

    /// <summary>A gateway refund may carry the reference of the payment it returns, and a refund is not a second payment.</summary>
    [Fact]
    public async Task GatewayRefundWithThePaymentsReference_IsAllowed()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();

        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayPayment);
        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayRefund);

        (await CountAsync(reference)).Should().Be(2);
    }

    /// <summary>Two gateways may report the same reference: the unique key is the method and the reference together.</summary>
    [Fact]
    public async Task GatewayPaymentsOfDifferentMethodsWithTheSameReference_AreAllowed()
    {
        var clientId = await SeedClientAsync();
        var reference = NewReference();

        await AddAsync(clientId, "bank-gateway", reference, TransactionSource.GatewayPayment);
        await AddAsync(clientId, "other-gateway", reference, TransactionSource.GatewayPayment);

        (await CountAsync(reference)).Should().Be(2);
    }

    /// <summary>
    /// Another unique violation is not a lost race on the ledger: the translation covers only the gateway
    /// payment index, so a duplicate department name stays the database error it is.
    /// </summary>
    [Fact]
    public async Task AnotherUniqueViolation_IsNotTranslatedIntoAConflict()
    {
        var name = $"Billing {Guid.NewGuid():N}";
        await AddDepartmentAsync(name);

        var act = () => AddDepartmentAsync(name);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.Should().NotBeOfType<ConcurrencyConflictException>();
        thrown.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be(Npgsql.PostgresErrorCodes.UniqueViolation);
    }

    /// <summary>Saves one department in a scope of its own.</summary>
    /// <param name="name">The department name, which is unique.</param>
    /// <returns>A task that completes once the department is saved.</returns>
    private async Task AddDepartmentAsync(string name)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Departments.Add(Department.Create(name, "billing@example.com"));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Saves one ledger row in a scope of its own, so each save is a separate writer.</summary>
    /// <param name="clientId">The owning client.</param>
    /// <param name="paymentMethod">The method recorded on the row.</param>
    /// <param name="reference">The reference recorded on the row.</param>
    /// <param name="source">The source recorded on the row.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    private async Task AddAsync(int clientId, string paymentMethod, string reference, TransactionSource source)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Transactions.Add(Transaction.Create(
            clientId, DateTimeOffset.UtcNow, "Payment", reference, null, paymentMethod, 10m, 0m, 0m, false, TestCurrencies.Base, source));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Counts the ledger rows that carry a reference.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>How many rows carry it.</returns>
    private async Task<int> CountAsync(string reference)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference, CancellationToken.None);
    }

    /// <summary>Seeds a client to own the ledger rows.</summary>
    /// <returns>The client id.</returns>
    private async Task<int> SeedClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>Builds a reference no other test shares.</summary>
    /// <returns>A fresh reference.</returns>
    private static string NewReference()
    {
        return $"gw-{Guid.NewGuid():N}";
    }
}
