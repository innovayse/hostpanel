namespace Innovayse.SDK.Base;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all domain registrar plugins.
/// Provides <see cref="GetConfig"/> and a structured <see cref="Logger"/>.
/// </summary>
public abstract class RegistrarProviderBase(
    string pluginId,
    IConfiguration configuration,
    ILogger logger) : IRegistrarPlugin
{
    /// <summary>Gets the structured logger pre-injected for this provider.</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Reads a configuration value from the Settings table via the standard key
    /// <c>integration:{pluginId}:{key}</c>.
    /// </summary>
    /// <param name="key">The field key as declared in <c>plugin.json</c>.</param>
    /// <returns>The stored value, or <see langword="null"/> if not set.</returns>
    protected string? GetConfig(string key)
        => configuration[$"integration:{pluginId}:{key}"];
}
