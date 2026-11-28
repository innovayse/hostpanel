using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reads the signing key a plugin was installed under from the plugin registry (<c>SignerKeyId</c> and <c>SignerPublicKey</c>
/// of its install record), for the plugin loader, which checks the signature of every installed plugin at startup and on a
/// reload, before the data context exists. The key is asked for only when the host's configured keys do not vouch for the
/// package, so a host that installs nothing from the registry never reads the database here.
/// </summary>
public static class PinnedSignerKeyReader
{
    /// <summary>Reads the key pinned to a plugin.</summary>
    /// <param name="connectionString">The product database's connection string.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="logger">Records why the key could not be read.</param>
    /// <returns>
    /// The key; <see langword="null"/> when the plugin has no install record or its record pins no key, and also when the
    /// database cannot be reached, the table does not exist yet or the read fails: not trusting is the safe answer to "may
    /// this signature load". A missing table logs at Information, any other failure at Error with its SQL state or exception type only.
    /// </returns>
    public static PluginSignerKey? Read(string? connectionString, string pluginId, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            using var connection = StartupDatabase.Open(connectionString);
            using var command = new NpgsqlCommand(
                "SELECT \"SignerKeyId\", \"SignerPublicKey\" FROM \"InstalledPlugins\" WHERE \"PluginId\" = @id AND \"SignerPublicKey\" IS NOT NULL", connection);
            command.Parameters.AddWithValue("id", pluginId);
            using var reader = command.ExecuteReader();
            return reader.Read() && !reader.IsDBNull(0) && !reader.IsDBNull(1)
                ? new PluginSignerKey(reader.GetString(0), reader.GetString(1))
                : null;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Expected on the first start: the migrations have not created the table yet, so no plugin has a pinned key.
            logger.LogInformation("The plugin install records do not exist yet (SQL state {SqlState}); no registry signing key is pinned.", ex.SqlState);
            return null;
        }
        catch (PostgresException ex)
        {
            logger.LogError("Could not read the pinned signing key of plugin {PluginId} (SQL state {SqlState}); only configured keys are trusted.", pluginId, ex.SqlState);
            return null;
        }
        catch (Exception ex)
        {
            // The exception text and stack can carry the connection string or the database host, so only the type is logged.
            logger.LogError("Could not read the pinned signing key of plugin {PluginId} ({ExceptionType}); only configured keys are trusted.", pluginId, ex.GetType().Name);
            return null;
        }
    }
}
