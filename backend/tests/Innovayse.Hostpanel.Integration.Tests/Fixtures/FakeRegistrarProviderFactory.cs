using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarProviderFactory"/> that hands back whatever provider a test registered
/// for a module, so a TLD import, a sync or a routing test never resolves a registrar plugin. A
/// fake port standing in for the plugin-backed factory production uses.
/// </summary>
public sealed class FakeRegistrarProviderFactory : IRegistrarProviderFactory
{
    /// <summary>The providers this factory answers with, keyed by module, compared without regard to letter case.</summary>
    private readonly Dictionary<string, IRegistrarProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers <paramref name="provider"/> as the one <paramref name="module"/> resolves to.</summary>
    /// <param name="module">The registrar module a command names.</param>
    /// <param name="provider">The provider to hand back for it.</param>
    public void Register(string module, IRegistrarProvider provider) => _providers[module] = provider;

    /// <inheritdoc/>
    public Task<IRegistrarProvider> GetProviderAsync(string? module, CancellationToken ct) =>
        module is not null && _providers.TryGetValue(module, out var provider)
            ? Task.FromResult(provider)
            : throw new ArgumentOutOfRangeException(
                nameof(module), module, "No fake registrar provider registered for this module.");
}
