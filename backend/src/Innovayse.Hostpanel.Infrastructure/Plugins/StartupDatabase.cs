using Npgsql;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Opens a short lived connection to the product database for the reads the plugin loader makes while the service
/// collection is still being built (and a reload makes long after), when the data context does not exist yet. A
/// database that is not up yet must not hold the start for long, so every wait is bounded.
/// </summary>
public static class StartupDatabase
{
    /// <summary>How long the connection and a query may take, in seconds.</summary>
    private const int TimeoutSeconds = 5;

    /// <summary>Opens a connection that is not pooled and gives up after <see cref="TimeoutSeconds"/>.</summary>
    /// <param name="connectionString">The product database's connection string; not blank.</param>
    /// <returns>The open connection; the caller disposes it.</returns>
    /// <exception cref="ArgumentException">The connection string cannot be parsed.</exception>
    public static NpgsqlConnection Open(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = TimeoutSeconds,
            CommandTimeout = TimeoutSeconds,
            Pooling = false,
        };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }
}
