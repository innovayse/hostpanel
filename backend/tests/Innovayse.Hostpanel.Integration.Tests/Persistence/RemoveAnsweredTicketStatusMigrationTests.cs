using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The <c>RemoveAnsweredTicketStatus</c> migration retires the <c>Answered</c> ticket status. A ticket row stores the
/// status as text, so a row written before the status was removed would no longer read back as a known value
/// unless the migration moves it to <c>AwaitingReply</c>.
/// </summary>
public sealed class RemoveAnsweredTicketStatusMigrationTests
{
    /// <summary>The last migration before the status was removed, the schema every database in production has until it is applied.</summary>
    private const string PreviousMigration = "20261106051103_AddCreditNotes";

    /// <summary>
    /// A ticket stored as <c>Answered</c> reads back as <see cref="TicketStatus.AwaitingReply"/> once the migration ran,
    /// and the tickets in every other status keep theirs.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Up_MovesAnAnsweredTicketToAwaitingReplyAndLeavesTheOthersAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var answeredId = await InsertTicketAsync(connectionString, "Answered");
            var openId = await InsertTicketAsync(connectionString, nameof(TicketStatus.Open));
            var closedId = await InsertTicketAsync(connectionString, nameof(TicketStatus.Closed));

            await context.Database.MigrateAsync();

            (await StatusOfAsync(connectionString, answeredId)).Should().Be(TicketStatus.AwaitingReply.ToString());
            (await StatusOfAsync(connectionString, openId)).Should().Be(TicketStatus.Open.ToString());
            (await StatusOfAsync(connectionString, closedId)).Should().Be(TicketStatus.Closed.ToString());
            (await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == answeredId)).Status
                .Should().Be(TicketStatus.AwaitingReply, "the entity reads the migrated row as a known status");
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Inserts a ticket row with the given status text, skipping the foreign-key checks on the client.</summary>
    /// <param name="connectionString">The database to write.</param>
    /// <param name="status">The status text to store, which may be one the code no longer knows.</param>
    /// <returns>The id of the new ticket.</returns>
    private static async Task<int> InsertTicketAsync(string connectionString, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // The client row is not the subject of the test and has many required columns, so the session skips the
        // foreign-key triggers for this one insert.
        command.CommandText =
            "SET session_replication_role = replica; " +
            "INSERT INTO \"Tickets\" (\"ClientId\", \"CreatedAt\", \"IsFlagged\", \"Message\", \"Priority\", \"Status\", \"Subject\") " +
            "VALUES (1, now(), false, 'Hello', 'Medium', @status, 'Subject') RETURNING \"Id\"";
        command.Parameters.AddWithValue("status", status);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Reads the stored status text of a ticket.</summary>
    /// <param name="connectionString">The database to read.</param>
    /// <param name="id">The ticket id.</param>
    /// <returns>The status text as it sits in the table.</returns>
    private static async Task<string> StatusOfAsync(string connectionString, int id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Status\" FROM \"Tickets\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
