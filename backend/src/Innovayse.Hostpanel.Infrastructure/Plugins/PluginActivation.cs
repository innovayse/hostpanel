using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What <see cref="PluginActivator"/> made of one loaded plugin.</summary>
/// <typeparam name="TContract">The SDK contract the plugin was activated as.</typeparam>
/// <param name="PluginId">The plugin id from its manifest.</param>
/// <param name="Outcome">How the activation ended; the caller decides how loudly a disabled plugin is logged.</param>
/// <param name="Instance">The activated plugin when <paramref name="Outcome"/> is <see cref="PluginActivationOutcome.Activated"/>; otherwise <see langword="null"/>.</param>
public sealed record PluginActivation<TContract>(string PluginId, PluginActivationOutcome Outcome, TContract? Instance)
    where TContract : class;
