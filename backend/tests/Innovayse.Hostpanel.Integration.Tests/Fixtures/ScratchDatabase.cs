using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A database created for one test on the shared server and dropped, with its pooled connections,
/// when the test is done.
/// </summary>
public sealed class ScratchDatabase : IAsyncDisposable
{
    /// <summary>The unique database name.</summary>
    private readonly string _name;

    /// <summary>The connection string of the database.</summary>
    public string ConnectionString { get; }

    /// <summary>Creates the handle for a database that already exists.</summary>
    /// <param name="name">The database name.</param>
    /// <param name="connectionString">The connection string that reaches it.</param>
    private ScratchDatabase(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
    }

    /// <summary>Creates an empty database on the shared server.</summary>
    /// <returns>The handle; dispose it to drop the database.</returns>
    public static async Task<ScratchDatabase> CreateAsync()
    {
        var name = $"it_{Guid.NewGuid():N}";

        return new ScratchDatabase(name, await SharedPostgresServer.CreateDatabaseAsync(name));
    }

    /// <summary>Runs one or several SQL statements.</summary>
    /// <param name="sql">The statements.</param>
    /// <returns>A task that completes when they have run.</returns>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a query and returns the first column of its first row as text.</summary>
    /// <param name="sql">The query.</param>
    /// <returns>The text of the value, or <see langword="null"/> when there is no row.</returns>
    public async Task<string?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        return (await command.ExecuteScalarAsync())?.ToString();
    }

    /// <summary>Lists every column of every table in Wolverine's schema.</summary>
    /// <returns>One <c>table.column</c> entry each, in a stable order.</returns>
    public async Task<List<string>> ColumnsOfWolverineSchemaAsync()
    {
        var columns = new List<string>();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema = 'wolverine' ORDER BY 1",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>Drops the database and the pool of connections to it.</summary>
    /// <returns>A task that completes when the database is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(connection);
        await SharedPostgresServer.DropDatabaseAsync(_name);
    }
}
