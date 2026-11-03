using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Reads the facts the plugin host API shares about one client from tables other modules own (clients, services,
/// invoices). A port, so the host API holds no data context: each method reads only the columns its record carries and
/// cuts its list in the query, so a client with thousands of rows never loads more than a plugin may see.
/// </summary>
public interface IPluginHostFactsReader
{
    /// <summary>Reads the name parts and owner subject of a client.</summary>
    /// <param name="clientId">Client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's profile parts, or <see langword="null"/> when no such client exists.</returns>
    Task<PluginHostClientDto?> FindClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Lists a client's newest services: at most <see cref="PluginHostLimits.MaxListed"/>, newest first (creation moment,
    /// then id, descending).
    /// </summary>
    /// <param name="clientId">Client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The services; empty when the client has none or does not exist.</returns>
    Task<IReadOnlyList<PluginHostServiceDto>> ListNewestServicesAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Lists a client's newest invoices the client may see (no drafts): at most <see cref="PluginHostLimits.MaxListed"/>,
    /// newest first (creation moment, then id, descending).
    /// </summary>
    /// <param name="clientId">Client primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoices; empty when the client has none or does not exist.</returns>
    Task<IReadOnlyList<PluginHostInvoiceDto>> ListNewestInvoicesAsync(int clientId, CancellationToken ct);
}
