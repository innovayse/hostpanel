using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for how <see cref="DomainReminder.ReminderType"/> is stored: the column keeps
/// the text rows were written with before the type became an enum, and each text reads back as its
/// <see cref="DomainReminderType"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainReminderStorageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A row inserted with the stored text reads back as the matching enum value.</summary>
    /// <param name="storedText">The text as it sits in the column.</param>
    /// <param name="expected">The reminder type it must read back as.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("30 Days Before Expiry", DomainReminderType.ThirtyDaysBeforeExpiry)]
    [InlineData("14 Days Before Expiry", DomainReminderType.FourteenDaysBeforeExpiry)]
    [InlineData("7 Days Before Expiry", DomainReminderType.SevenDaysBeforeExpiry)]
    [InlineData("1 Days Before Expiry", DomainReminderType.OneDayBeforeExpiry)]
    [InlineData("Expired Notice", DomainReminderType.ExpiredNotice)]
    [InlineData("Redemption Notice", DomainReminderType.RedemptionNotice)]
    [InlineData("No Such Reminder", DomainReminderType.Unknown)]
    public async Task StoredText_ReadsBackAsTheEnumAsync(string storedText, DomainReminderType expected)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainId = await SeedDomainAsync(context);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "DomainReminders" ("DomainId", "ReminderType", "SentTo", "SentAt") VALUES ({domainId}, {storedText}, {"legacy@reminders.test"}, {DateTimeOffset.UtcNow})""",
            CancellationToken.None);

        var reminder = await context.Set<DomainReminder>()
            .AsNoTracking()
            .SingleAsync(r => r.DomainId == domainId, CancellationToken.None);
        Assert.Equal(expected, reminder.ReminderType);
    }

    /// <summary>A reminder saved through the aggregate is written as its stored text, not the enum name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SavedReminder_IsWrittenAsTheStoredTextAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        domain.AddReminder(DomainReminderType.OneDayBeforeExpiry, "saved@reminders.test", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await context.Database
            .SqlQuery<string>($"""SELECT "ReminderType" AS "Value" FROM "DomainReminders" WHERE "DomainId" = {domain.Id}""")
            .SingleAsync(CancellationToken.None);
        Assert.Equal("1 Days Before Expiry", stored);
    }

    /// <summary>Seeds a client and a registered domain for the reminder rows to hang off.</summary>
    /// <param name="context">Data context.</param>
    /// <returns>The new domain's id.</returns>
    private static async Task<int> SeedDomainAsync(AppDbContext context)
    {
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain.Id;
    }
}
