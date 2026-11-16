using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the VAT number tests: a host whose VAT number port is a fake, a client seeded with a country, a number
/// and a recorded check, and a read of what the client holds afterwards.
/// </summary>
internal static class VatTestSeeds
{
    /// <summary>
    /// The moment a seeded verdict is recorded at by default: the real present, taken once, so it is as fresh as a check the
    /// daily re-check leaves alone (it only picks checks older than a week) and a test can compare a stored date with it.
    /// </summary>
    public static readonly DateTimeOffset FreshCheck = TestMoments.Present();

    /// <summary>What a client holds of its VAT number check, read back from the database.</summary>
    /// <param name="TaxId">The stored number.</param>
    /// <param name="Status">The stored verdict, or null.</param>
    /// <param name="CheckedAt">When it was recorded, or null.</param>
    /// <param name="Name">The registered name stored with a valid verdict, or null.</param>
    /// <param name="Address">The registered address stored with a valid verdict, or null.</param>
    public sealed record StoredVat(string? TaxId, VatCheckOutcome? Status, DateTimeOffset? CheckedAt, string? Name, string? Address);

    /// <summary>Builds a host of the shared factory whose VAT number port is the given fake.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="checker">The fake the host asks instead of a register.</param>
    /// <returns>The derived host.</returns>
    public static WebApplicationFactory<Program> HostWith(IntegrationTestFactory factory, FakeVatNumberChecker checker) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IVatNumberChecker>(checker)));

    /// <summary>Seeds a client with a billing country, a VAT number and optionally a recorded check, bypassing HTTP.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="country">The client's country code, or null for none.</param>
    /// <param name="taxId">The client's tax identification number, or null for none.</param>
    /// <param name="status">The verdict to record, or null for a number never checked.</param>
    /// <param name="taxExempt">Whether the client is tax exempt.</param>
    /// <param name="checkedAt">When the verdict was recorded, or null for <see cref="FreshCheck"/>.</param>
    /// <returns>The new client's id.</returns>
    public static async Task<int> SeedClientAsync(
        IntegrationTestFactory factory,
        string? country,
        string? taxId,
        VatCheckOutcome? status = null,
        bool taxExempt = false,
        DateTimeOffset? checkedAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create(
            $"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        client.UpdateAddress(null, null, null, null, null, country);
        client.UpdateSettings(true, taxExempt, taxId, taxExempt ? "Charity" : null, false, false, false, true, true);
        if (status is { } verdict)
        {
            client.RecordVatCheck(verdict, checkedAt ?? FreshCheck, verdict == VatCheckOutcome.Valid ? "Seeded Name" : null, null);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Removes a client's VAT number, bypassing HTTP, as if the client had changed it meanwhile.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>A task that completes when the number is gone.</returns>
    public static async Task RemoveTaxIdAsync(IntegrationTestFactory factory, int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.Clients.Where(c => c.Id == clientId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.TaxId, (string?)null), CancellationToken.None);
    }

    /// <summary>Reads what a client holds of its VAT number check.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The stored number and check.</returns>
    public static async Task<StoredVat> ReadAsync(IntegrationTestFactory factory, int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => new StoredVat(c.TaxId, c.VatStatus, c.VatCheckedAt, c.VatName, c.VatAddress))
            .SingleAsync();
    }
}
