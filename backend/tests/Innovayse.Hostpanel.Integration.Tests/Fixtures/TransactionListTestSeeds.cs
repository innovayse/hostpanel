using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders for the transaction list tests: ledger rows seeded straight into the database with a chosen date, description,
/// reference, payment method and amounts, and credit-only rows that moved no cash.
/// </summary>
public static class TransactionListTestSeeds
{
    /// <summary>Seeds one ledger row for a client.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The client the row belongs to.</param>
    /// <param name="currency">ISO 4217 code the amounts are in.</param>
    /// <param name="date">The moment of the transaction (UTC).</param>
    /// <param name="description">The description.</param>
    /// <param name="paymentMethod">The payment method.</param>
    /// <param name="amountIn">The amount received.</param>
    /// <param name="amountOut">The amount sent.</param>
    /// <param name="fees">The fees charged.</param>
    /// <param name="reference">The external reference; a unique one when not given.</param>
    /// <returns>The new transaction id.</returns>
    public static async Task<int> SeedAsync(
        WebApplicationFactory<Program> host,
        int clientId,
        string currency,
        DateTimeOffset date,
        string description,
        string paymentMethod = "manual",
        decimal amountIn = 10m,
        decimal amountOut = 0m,
        decimal fees = 0m,
        string? reference = null)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var transaction = Transaction.Create(
            clientId, date, description, reference ?? $"tx-{Guid.NewGuid():N}", null, paymentMethod,
            amountIn, amountOut, fees, false, currency, TransactionSource.Manual);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(CancellationToken.None);
        return transaction.Id;
    }

    /// <summary>Seeds one credit-only row, which moved the credit balance and no cash.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The client whose balance moved.</param>
    /// <param name="currency">ISO 4217 code the balance is held in.</param>
    /// <param name="date">The moment of the movement (UTC).</param>
    /// <param name="description">The description.</param>
    /// <returns>The new transaction id.</returns>
    public static async Task<int> SeedCreditOnlyAsync(
        WebApplicationFactory<Program> host, int clientId, string currency, DateTimeOffset date, string description)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var transaction = Transaction.CreateCreditMovement(
            clientId, date, description, null, currency, CreditMovementType.AdminAdd, 5m, 5m);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(CancellationToken.None);
        return transaction.Id;
    }
}
