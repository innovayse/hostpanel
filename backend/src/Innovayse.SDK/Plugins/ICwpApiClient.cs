namespace Innovayse.SDK.Plugins;

/// <summary>Abstraction over the CWP REST API client for provisioning and server info operations.</summary>
public interface ICwpApiClient
{
    /// <summary>Fetches server metadata including account count and CWP version.</summary>
    /// <param name="host">CWP server hostname or IP.</param>
    /// <param name="port">CWP API port (e.g. "2031").</param>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple with total account count and CWP version string.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    Task<(int AccountsCount, string CwpVersion)> GetServerInfoAsync(
        string host, string port, string apiKey, CancellationToken ct);
}
