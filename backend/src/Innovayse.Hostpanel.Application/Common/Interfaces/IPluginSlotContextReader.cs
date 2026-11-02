using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads what a plugin slot index needs from tables other modules own: the service and client of the page and the
/// operator's hidden widgets setting. A port, so the Plugins use cases never touch those tables directly.
/// </summary>
public interface IPluginSlotContextReader
{
    /// <summary>Finds a service without regard to who owns it, for the administrator's pages.</summary>
    /// <param name="serviceId">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service's client and product, or <see langword="null"/> when it does not exist.</returns>
    Task<PluginSlotServiceDto?> FindServiceAsync(int serviceId, CancellationToken ct);

    /// <summary>Whether a client account exists.</summary>
    /// <param name="clientId">Client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the client exists.</returns>
    Task<bool> ClientExistsAsync(int clientId, CancellationToken ct);

    /// <summary>Reads the stored value of the hidden slot widgets setting.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored value, or <see langword="null"/> when the row does not exist.</returns>
    Task<string?> ReadHiddenWidgetsAsync(CancellationToken ct);
}
