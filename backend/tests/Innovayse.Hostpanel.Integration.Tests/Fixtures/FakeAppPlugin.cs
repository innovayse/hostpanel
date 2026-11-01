using Innovayse.SDK.Plugins.Endpoints;
using Microsoft.Extensions.Configuration;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The test-only app plugin: records the call it receives on the <see cref="AppPluginProbe"/> and answers as the probe
/// says. It stands in for a plugin assembly, which a test must not load.
/// </summary>
internal sealed class FakeAppPlugin : IPluginEndpoints
{
    /// <summary>Where the call is recorded and the answer comes from.</summary>
    private readonly AppPluginProbe _probe;

    /// <summary>Initializes the plugin, which is the moment the probe learns a plugin was built.</summary>
    /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
    /// <param name="probe">Where the call is recorded and the answer comes from.</param>
    public FakeAppPlugin(IConfiguration configuration, AppPluginProbe probe)
    {
        _probe = probe;
        _probe.NoteConstructed();
    }

    /// <inheritdoc/>
    public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
    {
        _probe.Calls.Enqueue(call);
        return Task.FromResult(_probe.Behaviour(call));
    }
}
