using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Builds admin clients of a host whose plugin registry is an in-memory fake, for the registry use case tests.</summary>
internal static class PluginRegistryTestSeeds
{
    /// <summary>An admin client of a host that talks to <paramref name="registry"/> instead of the network.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="registry">The fake registry.</param>
    /// <param name="operatorKey">A key to add to the operator's trusted keys; <see langword="null"/> for none.</param>
    /// <returns>
    /// An authenticated admin client. The host trusts the test signing key and does not enforce signatures, as in
    /// Development and Testing, so a registry install being refused for a missing signature proves it is required anyway.
    /// </returns>
    public static async Task<HttpClient> AdminClientAsync(
        IntegrationTestFactory factory,
        FakePluginRegistryClient registry,
        TrustedPluginKey? operatorKey = null)
    {
        WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPluginRegistryClient>();
            services.AddSingleton<IPluginRegistryClient>(registry);
            if (operatorKey is not null)
            {
                services.Configure<PluginSigningOptions>(options => options.OperatorKeys.Add(operatorKey));
            }

            services.RemoveAll<PluginSignatureVerifier>();
            services.AddSingleton(PluginSigningTestSeeds.Verifier(enforced: false));
        }));

        return await PluginsTestSeeds.AdminClientAsync(factory, host);
    }

    /// <summary>Saves a setting through the admin settings endpoint.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>A task that completes once the value is stored.</returns>
    public static async Task SetAsync(HttpClient client, string key, string value)
    {
        var id = await AdminTestSeeds.FindSettingIdAsync(client, key);
        var response = await client.PutAsJsonAsync($"/api/admin/settings/{id}", new { value });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Saves the registry trust policy.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="trust">The policy.</param>
    /// <returns>A task that completes once the policy is stored.</returns>
    public static Task SetTrustAsync(HttpClient client, PluginRegistryTrust trust)
    {
        return SetAsync(client, PluginSettingKeys.RegistryTrust, trust.ToString());
    }

    /// <summary>Builds a signed plugin archive that declares a version.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="version">The version to declare.</param>
    /// <returns>The archive's raw bytes, signed by the trusted test key.</returns>
    public static byte[] SignedVersionedZip(string pluginId, string version)
    {
        var folder = PluginSigningTestSeeds.NewFolder("signed-versioned");
        try
        {
            PluginSigningTestSeeds.WritePackage(folder, pluginId);
            var manifest = Path.Combine(folder, "plugin.json");
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"version\": \"1.0.0\"", $"\"version\": \"{version}\""));
            PluginSigningTestSeeds.Sign(folder);
            return PluginSigningTestSeeds.Zip(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The body of a registry install request.</summary>
    /// <param name="version">The version to install.</param>
    /// <returns>The anonymous body.</returns>
    public static object InstallBody(string version)
    {
        return new { version };
    }
}
