using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>Hands out the demo registrar whichever registrar module is asked for.</summary>
public sealed class DemoRegistrarProviderFactory : IRegistrarProviderFactory
{
    /// <summary>The one fake registrar every module resolves to.</summary>
    private readonly DemoRegistrarProvider _provider;

    /// <summary>Creates the factory.</summary>
    /// <param name="provider">The one fake registrar every module resolves to.</param>
    public DemoRegistrarProviderFactory(DemoRegistrarProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public Task<IRegistrarProvider> GetProviderAsync(string? module, CancellationToken ct)
    {
        return Task.FromResult<IRegistrarProvider>(_provider);
    }
}
