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

    /// <summary>
    /// Reads the products the operator bound widgets to. A row of a deleted product is returned too, flagged as not
    /// existing, so the caller can tell a widget that was never bound from one whose products are all gone.
    /// </summary>
    /// <param name="pluginId">Only the bindings of this plugin; <see langword="null"/> for every plugin.</param>
    /// <param name="slotId">Only the bindings in this slot; <see langword="null"/> for every slot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per stored binding row; empty when nothing is bound.</returns>
    Task<IReadOnlyList<PluginSlotBindingDto>> ListBindingsAsync(string? pluginId, string? slotId, CancellationToken ct);

    /// <summary>Keeps the ids that belong to a product that exists, so an unknown or deleted id is dropped.</summary>
    /// <param name="productIds">The candidate product ids.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids among <paramref name="productIds"/> that name an existing product.</returns>
    Task<IReadOnlySet<int>> ExistingProductIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
}
