using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Endpoints;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One real scope whose disposal completes <see cref="Disposed"/>.</summary>
public sealed class RecordedPluginCallScope : IPluginCallScope
{
    /// <summary>The real scope.</summary>
    private readonly IPluginCallScope _inner;

    /// <summary>Completed once the real scope has been disposed.</summary>
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes the record.</summary>
    /// <param name="inner">The real scope.</param>
    public RecordedPluginCallScope(IPluginCallScope inner)
    {
        _inner = inner;
    }

    /// <summary>Gets a task that completes once the scope has been disposed.</summary>
    public Task Disposed => _disposed.Task;

    /// <inheritdoc/>
    public Task<Func<IPluginEndpoints>?> PrepareEndpointsAsync(string pluginId, CancellationToken ct)
    {
        return _inner.PrepareEndpointsAsync(pluginId, ct);
    }

    /// <inheritdoc/>
    public PluginCallServices CreateServices(PluginCallBinding binding)
    {
        return _inner.CreateServices(binding);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        _disposed.TrySetResult();
    }
}
