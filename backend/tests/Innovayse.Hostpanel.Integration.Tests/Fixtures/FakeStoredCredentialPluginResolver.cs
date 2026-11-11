using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IStoredCredentialPluginResolver"/> that hands back whatever capability a test registered for a module
/// id, or <see langword="null"/> for an unregistered one: the same "not installed, disabled or without the capability"
/// answer the real resolver gives. A fake port standing in for the plugin host, which a test must not load a real
/// plugin assembly through.
/// </summary>
public sealed class FakeStoredCredentialPluginResolver : IStoredCredentialPluginResolver
{
    /// <summary>The capabilities this resolver answers with, keyed by module id.</summary>
    private readonly Dictionary<string, IStoredCredentialPlugin> _plugins = [];

    /// <summary>
    /// Gets or sets work to run each time a module is resolved, with the module id; a test uses it to make something
    /// happen at a moment inside a use case (a client revoking a consent between two of its steps).
    /// </summary>
    public Func<string, Task>? OnResolve { get; set; }

    /// <summary>Registers <paramref name="plugin"/> as the capability <paramref name="module"/> resolves to.</summary>
    /// <param name="module">The plugin id a command names.</param>
    /// <param name="plugin">The capability to hand back for it.</param>
    public void Register(string module, IStoredCredentialPlugin plugin) => _plugins[module] = plugin;

    /// <summary>Forgets the capability of a module, as if the plugin had been switched off.</summary>
    /// <param name="module">The plugin id.</param>
    public void Unregister(string module) => _plugins.Remove(module);

    /// <inheritdoc/>
    public async Task<IStoredCredentialPlugin?> ResolveAsync(string module, CancellationToken ct)
    {
        if (OnResolve is not null)
        {
            await OnResolve(module);
        }

        return _plugins.GetValueOrDefault(module);
    }
}
