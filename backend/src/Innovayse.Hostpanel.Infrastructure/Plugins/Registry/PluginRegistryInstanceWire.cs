namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry JSON for a registered instance: its id and the challenge this installation has to serve.</summary>
/// <param name="Id">The instance id.</param>
/// <param name="Challenge">The text to serve at the well-known address.</param>
internal sealed record PluginRegistryInstanceWire(Guid? Id, string? Challenge);
