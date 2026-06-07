namespace Innovayse.SDK.Plugins;

/// <summary>Abstraction over the CWP7 REST API client for provisioning and server info operations.</summary>
public interface ICwp7ApiClient
{
    /// <summary>Fetches server metadata by listing packages to verify connectivity.</summary>
    /// <param name="host">CWP7 server hostname or IP.</param>
    /// <param name="port">CWP7 API port (e.g. "2304").</param>
    /// <param name="apiKey">CWP7 API key for authentication.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of packages configured on the server.</returns>
    /// <exception cref="HttpRequestException">Thrown when the API request fails.</exception>
    Task<int> GetServerInfoAsync(
        string host, string port, string apiKey, CancellationToken ct);
}
