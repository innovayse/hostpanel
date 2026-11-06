using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders and readers for the credit tests: raising a top-up through the client route, reading the credit history from the
/// ledger, and moving the admin's top-up limits for the length of one test.
/// </summary>
public static class CreditTestSeeds
{
    /// <summary>Raises a top-up invoice as the signed-in client through <c>POST /api/me/credit/top-ups</c>.</summary>
    /// <param name="owner">The client's own session.</param>
    /// <param name="amount">The amount to add.</param>
    /// <returns>The id of the new top-up invoice.</returns>
    public static async Task<int> TopUpAsync(HttpClient owner, decimal amount)
    {
        var created = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Reads the client's invoice from the database with its payments.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice.</returns>
    public static async Task<Invoice> InvoiceAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Items).Include(i => i.Transactions)
            .SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Reads the credit history of a client from the ledger, oldest first.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The ledger entries that moved the balance, oldest first.</returns>
    public static async Task<List<Transaction>> HistoryAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking()
            .Where(t => t.ClientId == clientId && t.CreditMovement != null)
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    /// <summary>Reads every ledger entry of a client, oldest first.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>All the client's ledger entries.</returns>
    public static async Task<List<Transaction>> LedgerAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().Where(t => t.ClientId == clientId).OrderBy(t => t.Id).ToListAsync();
    }

    /// <summary>Reads the client's own credit through <c>GET /api/me/credit</c>.</summary>
    /// <param name="owner">The client's own session.</param>
    /// <param name="query">An optional query string such as <c>?page=2</c>.</param>
    /// <returns>The credit body.</returns>
    public static async Task<JsonElement> MyCreditAsync(HttpClient owner, string query = "")
    {
        var response = await owner.GetAsync($"/api/me/credit{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Sets the two top-up limits and returns what puts them back.</summary>
    /// <param name="host">The host whose database holds the settings.</param>
    /// <param name="minimum">The least a client may add.</param>
    /// <param name="maximum">The most a client may add.</param>
    /// <returns>A disposable that restores the limits the test found.</returns>
    public static async Task<IAsyncDisposable> LimitsAsync(WebApplicationFactory<Program> host, string minimum, string maximum)
    {
        var restore = new RestoreLimits(host, await SwapAsync(host, minimum, maximum));
        return restore;
    }

    /// <summary>Writes both limits and reads back the values they replaced.</summary>
    /// <param name="host">The host whose database holds the settings.</param>
    /// <param name="minimum">The new minimum.</param>
    /// <param name="maximum">The new maximum.</param>
    /// <returns>The minimum and maximum that were stored before.</returns>
    private static async Task<(string Minimum, string Maximum)> SwapAsync(WebApplicationFactory<Program> host, string minimum, string maximum)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var low = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.CreditTopUpMinimum);
        var high = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.CreditTopUpMaximum);
        var before = (low.Value ?? string.Empty, high.Value ?? string.Empty);
        low.UpdateValue(minimum);
        high.UpdateValue(maximum);
        await context.SaveChangesAsync(CancellationToken.None);
        return before;
    }

    /// <summary>Puts the top-up limits back to what a test found when it is disposed.</summary>
    /// <param name="host">The host whose database holds the settings.</param>
    /// <param name="before">The minimum and maximum to put back.</param>
    private sealed class RestoreLimits(WebApplicationFactory<Program> host, (string Minimum, string Maximum) before) : IAsyncDisposable
    {
        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await SwapAsync(host, before.Minimum, before.Maximum);
        }
    }
}
