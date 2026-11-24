using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The unique index and the cascade of <c>TrialReminders</c> against Postgres. The rows are written with raw SQL, around
/// the aggregate, to prove the database enforces them.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TrialReminderConstraintTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Postgres state code of a unique violation.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>A second reminder of the same day for the same service is refused by the unique index.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheSameServiceAndDayCannotBeInsertedTwice()
    {
        var serviceId = await InsertServiceAsync();
        await InsertReminderAsync(serviceId, 3);

        var act = () => InsertReminderAsync(serviceId, 3);

        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;
        error.SqlState.Should().Be(UniqueViolation);
        error.ConstraintName.Should().Be("IX_TrialReminders_ServiceId_DaysBefore");
        (await InsertReminderAsync(serviceId, 1)).Should().Be(1);
    }

    /// <summary>Deleting the service deletes its reminders.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeletingTheServiceDeletesItsReminders()
    {
        var serviceId = await InsertServiceAsync();
        await InsertReminderAsync(serviceId, 3);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "ClientServices" WHERE "Id" = {serviceId}""");

        var left = await db.Database.SqlQuery<int>($"""SELECT 1 AS "Value" FROM "TrialReminders" WHERE "ServiceId" = {serviceId}""").CountAsync();
        left.Should().Be(0);
    }

    /// <summary>Seeds a client, a product and a pending service.</summary>
    /// <returns>The new service's id.</returns>
    private Task<int> InsertServiceAsync() => ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Pending);

    /// <summary>Inserts one reminder row straight into the table.</summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="daysBefore">The reminder day.</param>
    /// <returns>The number of rows inserted.</returns>
    private async Task<int> InsertReminderAsync(int serviceId, int daysBefore)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "TrialReminders" ("ServiceId", "DaysBefore", "SentAt") VALUES ({serviceId}, {daysBefore}, {DateTimeOffset.UtcNow})""");
    }
}
