using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Payment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IPaymentPluginResolver"/> that hands back whatever plugin a test registered for
/// a module id, or <see langword="null"/> for an unregistered one: the same "not loaded, not
/// configured, disabled" answer the real resolver gives. A fake port standing in for the plugin
/// host, which a test must not load a real plugin assembly through.
/// </summary>
public sealed class FakePaymentPluginResolver : IPaymentPluginResolver
{
    /// <summary>The plugins this resolver answers with, keyed by module id.</summary>
    private readonly Dictionary<string, IPaymentPlugin> _plugins = [];

    /// <summary>Registers <paramref name="plugin"/> as the one <paramref name="module"/> resolves to.</summary>
    /// <param name="module">The plugin id a command names.</param>
    /// <param name="plugin">The plugin to hand back for it.</param>
    public void Register(string module, IPaymentPlugin plugin) => _plugins[module] = plugin;

    /// <inheritdoc/>
    public Task<IPaymentPlugin?> ResolveAsync(string module, CancellationToken ct) =>
        Task.FromResult(_plugins.GetValueOrDefault(module));
}
