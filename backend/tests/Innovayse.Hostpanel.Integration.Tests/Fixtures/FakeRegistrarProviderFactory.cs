using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IRegistrarProviderFactory"/> that hands back whatever provider a test registered
/// for a module, so a TLD import or sync test never resolves a real registrar's HTTP client. A
/// fake port standing in for the DI-backed factory production uses, which resolves concrete
/// registrar types production credentials are needed for.
/// </summary>
public sealed class FakeRegistrarProviderFactory : IRegistrarProviderFactory
{
    /// <summary>The providers this factory answers with, keyed by module.</summary>
    private readonly Dictionary<RegistrarModule, IRegistrarProvider> _providers = [];

    /// <summary>Registers <paramref name="provider"/> as the one <paramref name="module"/> resolves to.</summary>
    /// <param name="module">The registrar module a command names.</param>
    /// <param name="provider">The provider to hand back for it.</param>
    public void Register(RegistrarModule module, IRegistrarProvider provider) => _providers[module] = provider;

    /// <inheritdoc/>
    public IRegistrarProvider GetProvider(RegistrarModule module) =>
        _providers.TryGetValue(module, out var provider)
            ? provider
            : throw new ArgumentOutOfRangeException(
                nameof(module), module, "No fake registrar provider registered for this module.");
}
