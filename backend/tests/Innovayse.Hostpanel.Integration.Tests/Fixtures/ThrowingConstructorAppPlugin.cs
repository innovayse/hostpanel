using Innovayse.SDK.Plugins.Endpoints;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A test-only app plugin whose constructor throws, for a plugin that fails before it can answer.</summary>
internal sealed class ThrowingConstructorAppPlugin : IPluginEndpoints
{
    /// <summary>The text the constructor throws with; a test asserts it never reaches a caller.</summary>
    public const string Secret = "constructor-secret-detail";

    /// <summary>Initializes the plugin by throwing.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public ThrowingConstructorAppPlugin(IConfiguration configuration)
    {
        throw new InvalidOperationException(Secret);
    }

    /// <inheritdoc/>
    public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
    {
        return Task.FromResult(PluginReply.NoContent());
    }
}
