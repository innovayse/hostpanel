namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// The connection of a service a plugin was granted: the service gives it out only for an active service that has an
/// account on a server, so both are always present. It carries the server's stored credentials through
/// <see cref="Server"/>, which masks them when printed, so this record has no secret member of its own.
/// </summary>
/// <param name="ServiceId">The service.</param>
/// <param name="ProvisioningRef">The account reference every provider call uses.</param>
/// <param name="Server">The server the service is assigned to.</param>
public sealed record GrantedPluginServiceConnection(int ServiceId, string ProvisioningRef, PluginServerAccess Server);
