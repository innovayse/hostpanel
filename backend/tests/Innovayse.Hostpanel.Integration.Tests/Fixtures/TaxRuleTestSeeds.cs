using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the tax rule tests: the admin route, a valid request body, creating and reading rules, seeding a
/// client with an address, and flipping the global tax switch, without repeating the plumbing in every test.
/// </summary>
internal static class TaxRuleTestSeeds
{
    /// <summary>The collection route of the tax rules admin API.</summary>
    public const string Route = "/api/admin/tax-rules";

    /// <summary>A valid request body for creating or updating a rule.</summary>
    /// <param name="country">The country code to send.</param>
    /// <param name="region">The region to send, or null for the whole country.</param>
    /// <param name="ratePercent">The rate to send.</param>
    /// <param name="isEnabled">Whether the rule is enabled.</param>
    /// <param name="name">The name to send, or <see langword="null"/> for a unique one.</param>
    /// <returns>The request body.</returns>
    public static object Body(
        string country = "DE", string? region = null, decimal ratePercent = 19m, bool isEnabled = true, string? name = null) => new
        {
            name = name ?? $"Rule {Guid.NewGuid():N}",
            country,
            region,
            ratePercent,
            isEnabled,
        };

    /// <summary>Creates a rule through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="country">The country code.</param>
    /// <param name="region">The region, or null for the whole country.</param>
    /// <param name="ratePercent">The rate in percent.</param>
    /// <param name="isEnabled">Whether the rule is enabled.</param>
    /// <returns>The new rule's id.</returns>
    public static Task<int> CreateAsync(
        HttpClient admin, string country, string? region = null, decimal ratePercent = 19m, bool isEnabled = true) =>
        StorefrontContentTestSeeds.CreateAsync(admin, Route, Body(country, region, ratePercent, isEnabled));

    /// <summary>Reads a rule through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="id">The rule's id.</param>
    /// <returns>The rule as the API answers it.</returns>
    public static async Task<JsonElement> GetAsync(HttpClient admin, int id) =>
        await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}");

    /// <summary>Seeds a client with a billing address and, optionally, the tax exemption.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="country">The client's country code, or null for none.</param>
    /// <param name="state">The client's state or region, or null for none.</param>
    /// <param name="taxExempt">Whether the client is tax exempt.</param>
    /// <returns>The new client's id.</returns>
    public static async Task<int> SeedClientAsync(
        IntegrationTestFactory factory, string? country, string? state = null, bool taxExempt = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create(
            $"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        client.UpdateAddress(null, null, null, state, null, country);
        if (taxExempt)
        {
            client.UpdateSettings(true, true, "EX-1", "Charity", false, false, false, true, true);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Stores the global tax switch.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="on">The value to store.</param>
    /// <returns>A task that completes when the setting is stored.</returns>
    public static async Task SetTaxEnabledAsync(IntegrationTestFactory factory, bool on)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.TaxEnabled);
        row.UpdateValue(on ? "true" : "false");
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
