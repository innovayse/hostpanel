namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// The one member of the registry error body this host reads: <c>type</c>, which holds the registry error code. Nothing else
/// of the body is read, kept or passed on, and a code that is not one of the known ones is treated as unknown.
/// </summary>
/// <param name="Type">The registry error code, such as <c>INSTANCE_REVOKED_FORBIDDEN</c>.</param>
internal sealed record PluginRegistryErrorWire(string? Type);
