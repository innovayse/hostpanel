using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The registrar factory of the test host: whichever module is asked for, and even a name with no module configured, it hands
/// back the one provider registered in the container (a no-op stub unless a test replaces it), so a test that is not about
/// registrar routing never reaches a registrar plugin. The provider arrives through the constructor, so Wolverine's generated
/// handler code builds the factory like any other.
/// </summary>
public sealed class StubRegistrarProviderFactory : IRegistrarProviderFactory
{
    /// <summary>The provider registered in the container.</summary>
    private readonly IRegistrarProvider _provider;

    /// <summary>Initializes the factory.</summary>
    /// <param name="provider">The provider registered in the container.</param>
    public StubRegistrarProviderFactory(IRegistrarProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public Task<IRegistrarProvider> GetProviderAsync(string? module, CancellationToken ct) => Task.FromResult(_provider);
}
