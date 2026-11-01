using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What <see cref="PluginActivator"/> found out about one loaded plugin before building it.</summary>
/// <typeparam name="TContract">The SDK contract the plugin would be activated as.</typeparam>
/// <param name="PluginId">The plugin id from its manifest.</param>
/// <param name="Outcome">How the gates ended; the caller decides how loudly a disabled plugin is logged.</param>
/// <param name="Create">
/// Builds the plugin when <paramref name="Outcome"/> is <see cref="PluginActivationOutcome.Activated"/>; otherwise
/// <see langword="null"/>. It runs the plugin's constructor and throws what the constructor throws.
/// </param>
public sealed record PreparedPluginActivation<TContract>(string PluginId, PluginActivationOutcome Outcome, Func<TContract>? Create)
    where TContract : class;
