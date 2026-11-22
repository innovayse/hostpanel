using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// One hosting service as a plugin's service connection read sees it: whose it is, which product, its state and
/// provisioning reference, and the server it is assigned to. It carries the server's stored credentials through
/// <see cref="Server"/>, which masks them when printed, so this record has no secret member of its own.
/// </summary>
/// <param name="ServiceId">The service.</param>
/// <param name="ClientId">The client that owns the service; the gate compares it with the call's client.</param>
/// <param name="ProductId">The product the service was ordered from.</param>
/// <param name="Status">The service's lifecycle state.</param>
/// <param name="ProvisioningRef">
/// The account reference every provider call uses, or <see langword="null"/> when none is set.
/// </param>
/// <param name="Server">
/// The assigned server, or <see langword="null"/> when the service has none or it was deleted.
/// </param>
public sealed record PluginServiceConnection(
    int ServiceId,
    int ClientId,
    int ProductId,
    ServiceStatus Status,
    string? ProvisioningRef,
    PluginServerAccess? Server);
