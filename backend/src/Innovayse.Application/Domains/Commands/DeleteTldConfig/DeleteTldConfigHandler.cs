namespace Innovayse.Application.Domains.Commands.DeleteTldConfig;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handles <see cref="DeleteTldConfigCommand"/> by loading the TLD configuration,
/// removing it from the repository, and persisting the deletion.
/// Invalidates the TLD pricing cache after deletion.
/// </summary>
/// <param name="repo">TLD configuration repository for loading and removal.</param>
/// <param name="uow">Unit of work for committing the deletion.</param>
/// <param name="cache">Memory cache for invalidating pricing data.</param>
public sealed class DeleteTldConfigHandler(ITldConfigRepository repo, IUnitOfWork uow, IMemoryCache cache)
{
    /// <summary>
    /// Deletes a TLD configuration permanently from the persistence store.
    /// </summary>
    /// <param name="cmd">The delete command containing the TLD configuration ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no TLD configuration with the given ID is found.
    /// </exception>
    public async Task HandleAsync(DeleteTldConfigCommand cmd, CancellationToken ct)
    {
        var entity = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"TLD configuration {cmd.Id} not found.");

        repo.Remove(entity);
        await uow.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients no longer see the deleted TLD
        cache.Remove("tld-pricing-configs");
    }
}
