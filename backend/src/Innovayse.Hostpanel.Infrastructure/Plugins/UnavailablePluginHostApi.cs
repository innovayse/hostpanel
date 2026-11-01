using Innovayse.SDK.Plugins.Host;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The host facts of a call before the host shares any: every member says so by throwing, which the plugin runner
/// answers as <c>PLUGIN_FAILED</c> and logs with the plugin id. Answering an empty profile or empty lists instead would
/// tell a plugin that a client has no services or invoices when the truth is that the host cannot say yet.
/// </summary>
public sealed class UnavailablePluginHostApi : IPluginHostApi
{
    /// <summary>The sentence every member throws with.</summary>
    public const string Reason = "The host does not share client facts with plugins yet.";

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host shares no client facts yet.</exception>
    public Task<PluginClientProfile?> GetProfileAsync(CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host shares no client facts yet.</exception>
    public Task<IReadOnlyList<PluginClientService>> ListServicesAsync(CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the host shares no client facts yet.</exception>
    public Task<IReadOnlyList<PluginClientInvoice>> ListInvoicesAsync(CancellationToken ct)
    {
        throw new NotSupportedException(Reason);
    }
}
