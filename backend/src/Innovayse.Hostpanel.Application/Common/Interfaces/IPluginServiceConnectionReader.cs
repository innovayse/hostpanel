using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads a hosting service together with the server it is assigned to, credentials included, for the plugin host API's
/// service connection. It takes no client id: ownership is judged by <see cref="PluginHostingGate"/>, like the SSH
/// context resolver, and the credentials are read on this path alone.
/// </summary>
public interface IPluginServiceConnectionReader
{
    /// <summary>Reads the service and its server.</summary>
    /// <param name="serviceId">The service the plugin asked about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service, or <see langword="null"/> when it does not exist.</returns>
    public Task<PluginServiceConnection?> ResolveAsync(int serviceId, CancellationToken ct);
}
