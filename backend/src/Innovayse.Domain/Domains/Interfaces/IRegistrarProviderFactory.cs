namespace Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Factory for resolving <see cref="IRegistrarProvider"/> implementations by <see cref="RegistrarModule"/>.
/// Allows the application layer to obtain the correct provider at runtime without depending on Infrastructure.
/// </summary>
public interface IRegistrarProviderFactory
{
    /// <summary>
    /// Returns the <see cref="IRegistrarProvider"/> implementation for the specified registrar module.
    /// </summary>
    /// <param name="module">The registrar module whose provider to resolve.</param>
    /// <returns>The corresponding <see cref="IRegistrarProvider"/> implementation.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown when <paramref name="module"/> does not have a registered provider.
    /// </exception>
    IRegistrarProvider GetProvider(RegistrarModule module);
}
