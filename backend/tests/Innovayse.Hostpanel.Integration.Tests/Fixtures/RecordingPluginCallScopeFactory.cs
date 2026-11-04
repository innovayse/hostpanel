using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The host's own <see cref="PluginCallScopeFactory"/>, wrapped so a test can see when each scope it opened is disposed:
/// every scope is still the real one, and only its disposal is recorded. A test awaits that signal instead of probing a
/// disposed data context, which would race the disposal itself.
/// </summary>
public sealed class RecordingPluginCallScopeFactory : IPluginCallScopeFactory
{
    /// <summary>The real factory every scope comes from.</summary>
    private readonly PluginCallScopeFactory _inner;

    /// <summary>Initializes the factory over the real one.</summary>
    /// <param name="scopes">The container's scope factory, handed to the real factory.</param>
    public RecordingPluginCallScopeFactory(IServiceScopeFactory scopes)
    {
        _inner = new PluginCallScopeFactory(scopes);
    }

    /// <summary>Gets every scope opened since the last <see cref="Reset"/>, in order.</summary>
    public ConcurrentQueue<RecordedPluginCallScope> Opened { get; } = new();

    /// <summary>Forgets the scopes recorded so far; each test starts from here.</summary>
    public void Reset()
    {
        Opened.Clear();
    }

    /// <inheritdoc/>
    public IPluginCallScope Open()
    {
        var recorded = new RecordedPluginCallScope(_inner.Open());
        Opened.Enqueue(recorded);
        return recorded;
    }
}
