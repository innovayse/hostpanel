using Innovayse.SDK.Plugins.StoredCredentials;


namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves the optional stored credential capability of a payment gateway module. Built on
/// <see cref="IPaymentPluginResolver"/>, so a plugin passes the same gates (installed, enabled, configured) before
/// its capability is offered.
/// </summary>
public interface IStoredCredentialPluginResolver
{
    /// <summary>Resolves the stored credential capability for the given module.</summary>
    /// <param name="module">The payment plugin id (for example "bank-gateway").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The capability, or <see langword="null"/> when the module does not resolve to a payment plugin (not installed,
    /// disabled or misconfigured) or its plugin does not implement <see cref="IStoredCredentialPlugin"/>.
    /// </returns>
    Task<IStoredCredentialPlugin?> ResolveAsync(string module, CancellationToken ct);
}
