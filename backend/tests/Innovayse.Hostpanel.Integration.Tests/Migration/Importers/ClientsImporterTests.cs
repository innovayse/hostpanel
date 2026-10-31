using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="ClientsImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: provisions a new local account for a plugin
/// client record not already known, then creates its client row with the source's status, signup
/// date and tax setting.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientsImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client record the host does not already know is provisioned and imported.</summary>
    [Fact]
    public async Task RunAsync_Clients_ImportsANewClientAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var email = $"migration-client-{Guid.NewGuid():N}@example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(client);

        // Idempotent re-import: the email now resolves to an existing client, so the second run
        // skips it rather than provisioning a duplicate account.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var clients = await context.Clients.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
        Assert.Single(clients);
    }

    /// <summary>A malformed source currency does not lose the client: it is imported without a currency.</summary>
    [Fact]
    public async Task RunAsync_ClientWithMalformedCurrency_IsImportedWithoutACurrencyAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var email = $"migration-client-{Guid.NewGuid():N}@example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null, currency: "12!"));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Null(client.Currency);
    }

    /// <summary>A padded, lower-case source currency is stored trimmed and upper-cased.</summary>
    [Fact]
    public async Task RunAsync_ClientWithPaddedCurrency_IsImportedNormalisedAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var email = $"migration-client-{Guid.NewGuid():N}@example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null, currency: " eur "));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal("EUR", client.Currency);
    }

    /// <summary>
    /// An account left without a client row by an earlier pull gets its client row, rather than
    /// being skipped as already existing forever.
    /// </summary>
    [Fact]
    public async Task RunAsync_AccountWithoutClient_CreatesTheClientAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = $"migration-orphan-{Guid.NewGuid():N}@example.test";
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, FirstName = "Jane", LastName = "Doe", EmailConfirmed = true };
        Assert.True((await userManager.CreateAsync(user, "Pass@123!")).Succeeded);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.True(await context.Clients.AsNoTracking().AnyAsync(c => c.UserId == user.Id));
        var log = await context.MigrationLogs.AsNoTracking().SingleAsync(l => l.JobId == job.Id);
        Assert.Equal(MigrationLogAction.Imported, log.Action);
    }

    /// <summary>
    /// A closed, tax exempt source client keeps its status and signup date, and the tax flag
    /// leaves the overdue notice setting at its default.
    /// </summary>
    [Fact]
    public async Task RunAsync_ClosedTaxExemptClient_KeepsSourceStatusDateAndSettingsAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var email = $"migration-closed-{Guid.NewGuid():N}@example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Closed", taxExempt: true, signupDate: "2019-03-04T05:06:07Z"));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(ClientStatus.Closed, client.Status);
        Assert.Equal(new DateTimeOffset(2019, 3, 4, 5, 6, 7, TimeSpan.Zero), client.CreatedAt);
        Assert.True(client.TaxExempt);
        Assert.True(client.OverdueNotices);
    }

    /// <summary>A source client with no first name is imported under the local part of its email, not lost.</summary>
    [Fact]
    public async Task RunAsync_ClientWithoutFirstName_UsesTheEmailLocalPartAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var localPart = $"nameless-{Guid.NewGuid():N}";
        var email = $"{localPart}@example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null, firstName: string.Empty));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var userId = await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email);
        var client = await context.Clients.AsNoTracking().SingleAsync(c => c.UserId == userId);
        Assert.Equal(localPart, client.FirstName);
    }

    /// <summary>A record with no first name and no usable email is skipped as <c>NameMissing</c>, and no account is created for it.</summary>
    [Fact]
    public async Task RunAsync_ClientWithNoNameAtAll_IsSkippedAsNameMissingAsync()
    {
        var source = NewSource();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Clients);
        var email = $"@nameless-{Guid.NewGuid():N}.example.test";
        source.SetDatasetJson("clients", ClientJson(email, "Active", taxExempt: false, signupDate: null, firstName: string.Empty));

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var log = await context.MigrationLogs.AsNoTracking().SingleAsync(l => l.JobId == job.Id);
        Assert.Equal(MigrationLogAction.Skipped, log.Action);
        Assert.Equal(MigrationSkipReason.NameMissing, log.SkipReason);
        Assert.Equal(string.Empty, await MigrationTestSeeds.UserIdForEmailAsync(scope.ServiceProvider, email));
    }

    /// <summary>A fake source whose totals announce one client.</summary>
    /// <returns>The fake source.</returns>
    private static FakeMigrationPullSource NewSource() =>
        new() { Totals = MigrationSourceTotals.Empty with { Accounts = new MigrationAccountTotals(Clients: 1) } };

    /// <summary>The <c>clients</c> dataset holding one client.</summary>
    /// <param name="email">The client's email.</param>
    /// <param name="status">The source status word.</param>
    /// <param name="taxExempt">Whether the client is tax exempt.</param>
    /// <param name="signupDate">The signup moment as ISO 8601 text, or null when the source holds none.</param>
    /// <param name="firstName">The client's first name in the source.</param>
    /// <param name="currency">The source currency text, or null when the source sends none.</param>
    /// <returns>The dataset JSON.</returns>
    private static string ClientJson(string email, string status, bool taxExempt, string? signupDate, string firstName = "Jane", string? currency = null)
    {
        var currencyJson = currency is null ? "null" : $"\"{currency}\"";
        var signup = signupDate is null ? "null" : $"\"{signupDate}\"";
        var exempt = taxExempt ? "true" : "false";
        return $$"""
            [{"Email":"{{email}}","FirstName":"{{firstName}}","LastName":"Doe","Company":null,"Phone":null,"Address":null,"City":null,"State":null,"PostCode":null,"Country":null,"Status":"{{status}}","Currency":{{currencyJson}},"TaxExempt":{{exempt}},"CreditBalance":0,"SignupDate":{{signup}},"LastLogin":null}]
            """;
    }
}
