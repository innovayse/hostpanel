namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves the <see cref="IRegistrarProvider"/> that answers for a registrar module, which is the id of
/// the registrar plugin. Allows the application layer to obtain the provider at runtime without depending
/// on Infrastructure.
/// </summary>
public interface IRegistrarProviderFactory
{
    /// <summary>
    /// Returns the provider for the specified registrar module. A module whose plugin is not installed, not
    /// switched on, not configured or not a registrar, and a missing module, yield a provider that refuses
    /// every operation, so the caller never needs a separate "not available" branch.
    /// </summary>
    /// <param name="module">
    /// The registrar module (the plugin id, compared case-insensitively), or <see langword="null"/> when
    /// nothing is configured for the name.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider that holds the module's domains.</returns>
    public Task<IRegistrarProvider> GetProviderAsync(string? module, CancellationToken ct);
}
