namespace Innovayse.SDK.Plugins;

/// <summary>Abstraction over the CWP REST API client for provisioning and server info operations.</summary>
public interface ICwpApiClient
{
    /// <summary>
    /// The port a CWP server answers its REST API on when a deployment has not configured one.
    /// </summary>
    /// <remarks>
    /// Declared on the contract rather than on an implementation because both sides of it need
    /// the same number and neither can see the other's: the provider assembly is loaded
    /// reflectively, so the Application layer cannot reference a constant inside it, and the
    /// Application layer is where the "no port configured" fallback is actually applied.
    /// <para>
    /// <b>2304, not 2031.</b> 2031 is the CWP panel's own HTTPS user interface; it answers
    /// nothing under <c>/v1/</c>, which is the only path anything built from this constant
    /// requests. Every other consumer in this repository — <c>CwpApiClient</c>,
    /// <c>Cwp7ApiClient</c>, <c>ProvisioningProviderFactory</c>, <c>ServerConnectionTester</c> —
    /// already used 2304; <c>GetCwpServerInfoHandler</c> was the last holder of a 2031 fallback,
    /// and a deployment that had configured a host and an API key but no port therefore probed
    /// the UI port and reported the server as unreachable.
    /// </para>
    /// </remarks>
    public const int DefaultApiPort = 2304;

    /// <summary>Fetches server metadata including account count and CWP version.</summary>
    /// <param name="host">CWP server hostname or IP.</param>
    /// <param name="port">CWP API port (e.g. "2304").</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple with total account count and CWP version string.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    Task<(int AccountsCount, string CwpVersion)> GetServerInfoAsync(
        string host, string port, string apiKey, CancellationToken ct);
}
