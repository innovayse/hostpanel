using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads the admin setting "allow unsigned plugins" while the service collection is still being built, which is when
/// the plugin loader has to decide which plugins load. The data context does not exist yet then, so this is the one
/// place that reads a settings row straight from the database. The same row is read through the data context on
/// every install; both read <see cref="PluginSettingKeys.AllowUnsigned"/> and interpret it with
/// <see cref="PluginSettingKeys.IsOn"/>.
/// </summary>
public static class AllowUnsignedSettingReader
{
    /// <summary>How long the connection and the query may take: a database that is not up yet must not hold the start for long.</summary>
    private const int TimeoutSeconds = 5;

    /// <summary>Reads the setting.</summary>
    /// <param name="connectionString">The product database's connection string.</param>
    /// <param name="logger">Records why the setting could not be read.</param>
    /// <returns>
    /// <see langword="true"/> only when the row exists and is on. A database that cannot be reached, a table that
    /// does not exist yet (the first start, before migrations), a connection string that cannot be parsed, any other
    /// failure, or a row that does not exist read as off: refusing is
    /// the safe answer to "may unsigned plugins load".
    /// </returns>
    public static bool Read(string? connectionString, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = TimeoutSeconds,
                CommandTimeout = TimeoutSeconds,
                Pooling = false,
            };
            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = new NpgsqlCommand("SELECT \"Value\" FROM \"Settings\" WHERE \"Key\" = @key", connection);
            command.Parameters.AddWithValue("key", PluginSettingKeys.AllowUnsigned);
            return PluginSettingKeys.IsOn(command.ExecuteScalar() as string);
        }
        catch (Exception ex)
        {
            // Any failure reads as off. Only the setting name and the exception type are logged: the exception text and
            // stack can carry the connection string or the database host.
            logger.LogWarning(
                "Could not read the {Setting} setting at startup ({ExceptionType}); unsigned plugins are not allowed to load.",
                PluginSettingKeys.AllowUnsigned,
                ex.GetType().Name);
            return false;
        }
    }
}
