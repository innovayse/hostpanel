namespace Innovayse.Infrastructure.Domains;

using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Infrastructure.Domains.NameAm;
using Innovayse.Infrastructure.Domains.Namecheap;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves <see cref="IRegistrarProvider"/> implementations by <see cref="RegistrarModule"/>.
/// Delegates to the DI container so each provider receives its own dependencies (HTTP clients, settings, etc.).
/// </summary>
/// <param name="serviceProvider">The scoped service provider for resolving concrete provider types.</param>
public sealed class RegistrarProviderFactory(IServiceProvider serviceProvider) : IRegistrarProviderFactory
{
    /// <summary>
    /// Returns the <see cref="IRegistrarProvider"/> implementation for the specified registrar module.
    /// </summary>
    /// <param name="module">The registrar module whose provider to resolve.</param>
    /// <returns>The corresponding <see cref="IRegistrarProvider"/> implementation.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="module"/> has no registered provider.</exception>
    public IRegistrarProvider GetProvider(RegistrarModule module)
    {
        return module switch
        {
            RegistrarModule.NameAm => serviceProvider.GetRequiredService<NameAmRegistrarProvider>(),
            RegistrarModule.Namecheap => serviceProvider.GetRequiredService<NamecheapRegistrarProvider>(),
            _ => throw new NotSupportedException($"No registrar provider for module '{module}'."),
        };
    }
}
