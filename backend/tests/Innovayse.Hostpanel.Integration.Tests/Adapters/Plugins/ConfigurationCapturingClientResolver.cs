using System.Security.Claims;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>Minimal identity plugin used as a registered implementation type; keeps the configuration it was activated with.</summary>
internal sealed class ConfigurationCapturingClientResolver : IClientResolver
{
    /// <summary>Initializes the fake with the composed configuration for assertions.</summary>
    /// <param name="configuration">The configuration the activator composed.</param>
    public ConfigurationCapturingClientResolver(IConfiguration configuration) => Configuration = configuration;

    /// <summary>Gets the configuration the activator passed in.</summary>
    public IConfiguration Configuration { get; }

    /// <inheritdoc/>
    public Task<ClientAccess?> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct)
        => throw new NotSupportedException();
}
