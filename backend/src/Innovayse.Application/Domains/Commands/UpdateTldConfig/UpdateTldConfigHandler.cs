namespace Innovayse.Application.Domains.Commands.UpdateTldConfig;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handles <see cref="UpdateTldConfigCommand"/> by loading the existing TLD configuration,
/// applying field and pricing updates, and persisting the changes.
/// Invalidates the TLD pricing cache after updates to ensure clients see fresh prices.
/// </summary>
/// <param name="repo">TLD configuration repository for loading the entity.</param>
/// <param name="uow">Unit of work for committing changes.</param>
/// <param name="cache">Memory cache for invalidating pricing data.</param>
public sealed class UpdateTldConfigHandler(ITldConfigRepository repo, IUnitOfWork uow, IMemoryCache cache)
{
    /// <summary>
    /// Updates an existing TLD configuration's settings, cost prices, and sell prices.
    /// </summary>
    /// <param name="cmd">The update TLD configuration command containing the new field values and pricing.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no TLD configuration with the given ID is found.
    /// </exception>
    public async Task HandleAsync(UpdateTldConfigCommand cmd, CancellationToken ct)
    {
        var entity = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"TLD configuration {cmd.Id} not found.");

        var module = Enum.Parse<RegistrarModule>(cmd.RegistrarModule);

        entity.Update(
            module,
            cmd.Currency,
            cmd.SellCurrency,
            cmd.IsEnabled,
            cmd.SortOrder,
            cmd.Categories);

        entity.UpdateCostPrices(cmd.CostRegister, cmd.CostTransfer, cmd.CostRenew);
        entity.UpdateSellPrices(cmd.SellRegister, cmd.SellTransfer, cmd.SellRenew);

        await uow.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see fresh prices
        cache.Remove("tld-pricing-configs");
    }
}
