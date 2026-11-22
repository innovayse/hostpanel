using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>What the host requires of one capability a manifest may declare, as <see cref="PluginCapabilityRules"/> keeps it.</summary>
/// <param name="Contract">The SDK interface a plugin class must implement for the capability to be true.</param>
/// <param name="OnlyType">The one plugin type that may declare the capability, or <see langword="null"/> when every type may.</param>
/// <param name="Requires">The capability the plugin must declare as well, or <see langword="null"/> when it stands alone.</param>
internal sealed record PluginCapabilityRule(Type Contract, PluginType? OnlyType, string? Requires);
