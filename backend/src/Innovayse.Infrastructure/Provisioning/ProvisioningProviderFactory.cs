namespace Innovayse.Infrastructure.Provisioning;

using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers;
using Innovayse.Providers.CWP7;
using Microsoft.Extensions.Logging;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Resolves the correct provider implementation based on the server's <see cref="Server.Module"/>.
/// </summary>
/// <param name="httpClientFactory">Factory for creating HTTP clients for provider API calls.</param>
/// <param name="loggerFactory">Factory for creating typed loggers for each provider.</param>
public sealed class ProvisioningProviderFactory(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IProvisioningProviderFactory
{
    /// <summary>Default CWP7 API port.</summary>
    private const int DefaultCwp7Port = 2304;

    /// <summary>
    /// Creates a provisioning provider configured with the credentials of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown for unsupported server modules.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required credentials are missing.</exception>
    public IProvisioningProvider CreateFor(Server server)
    {
        return server.Module switch
        {
            ServerModule.Cwp7 => CreateCwp7Provider(server),
            _ => throw new NotSupportedException($"No provisioning provider for module '{server.Module}'. Only CWP7 is currently supported via the factory."),
        };
    }

    /// <summary>
    /// Creates a CWP7 provider for the given server.
    /// </summary>
    /// <param name="server">The CWP7 server.</param>
    /// <returns>A configured <see cref="Cwp7ProvisioningProvider"/>.</returns>
    private Cwp7ProvisioningProvider CreateCwp7Provider(Server server)
    {
        var apiKey = server.AccessHash
            ?? throw new InvalidOperationException($"Server '{server.Name}' (id={server.Id}) has no access hash configured.");

        var host = $"https://{server.Hostname}:{DefaultCwp7Port}";
        var serverIp = server.IpAddress ?? server.Hostname;

        var httpClient = httpClientFactory.CreateClient("Cwp7");

        var client = new Cwp7ApiClient(httpClient, loggerFactory.CreateLogger<Cwp7ApiClient>());

        return new Cwp7ProvisioningProvider(
            host, apiKey, serverIp, client,
            loggerFactory.CreateLogger<Cwp7ProvisioningProvider>());
    }
}
