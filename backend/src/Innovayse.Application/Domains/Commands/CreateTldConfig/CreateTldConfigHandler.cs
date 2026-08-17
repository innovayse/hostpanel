namespace Innovayse.Application.Domains.Commands.CreateTldConfig;

using Innovayse.Application.Common;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Handles <see cref="CreateTldConfigCommand"/> by validating uniqueness,
/// creating a new <see cref="TldConfig"/> aggregate with prices, and persisting it.
/// Invalidates the TLD pricing cache after creation.
/// </summary>
/// <param name="repo">TLD configuration repository for persistence and uniqueness checks.</param>
/// <param name="uow">Unit of work for committing changes.</param>
/// <param name="cache">Memory cache for invalidating pricing data.</param>
public sealed class CreateTldConfigHandler(ITldConfigRepository repo, IUnitOfWork uow, IMemoryCache cache)
{
    /// <summary>
    /// Creates and persists a new TLD configuration with cost and sell pricing.
    /// </summary>
    /// <param name="cmd">The create TLD configuration command with all settings and pricing data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly assigned TLD configuration identifier.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a TLD configuration with the same TLD extension already exists.
    /// </exception>
    public async Task<int> HandleAsync(CreateTldConfigCommand cmd, CancellationToken ct)
    {
        // TryParse, not Parse: Parse raises ArgumentException, which no handler in the
        // pipeline classifies, so a misspelled module came back as
        // 500 "An unexpected error occurred." — indistinguishable from the server
        // falling over. InvalidOperationException already maps to 400, and naming the
        // accepted values saves the caller guessing at their casing.
        if (!Enum.TryParse<RegistrarModule>(cmd.RegistrarModule, ignoreCase: true, out var module))
        {
            throw new InvalidOperationException(
                $"Unknown registrar module '{cmd.RegistrarModule}'. Accepted values: "
                + string.Join(", ", Enum.GetNames<RegistrarModule>()) + ".");
        }
        var normalizedTld = cmd.Tld.ToLower().Trim();

        var existing = await repo.FindByTldAsync(normalizedTld, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"TLD configuration for '{normalizedTld}' already exists.");
        }

        var entity = TldConfig.Create(
            normalizedTld,
            module,
            cmd.Currency,
            cmd.SellCurrency,
            cmd.IsEnabled,
            cmd.SortOrder,
            cmd.Categories);

        entity.UpdateCostPrices(cmd.CostRegister, cmd.CostTransfer, cmd.CostRenew);
        entity.UpdateSellPrices(cmd.SellRegister, cmd.SellTransfer, cmd.SellRenew);

        repo.Add(entity);
        await uow.SaveChangesAsync(ct);

        // Invalidate the TLD pricing cache so clients see the new TLD
        cache.Remove("tld-pricing-configs");

        return entity.Id;
    }
}
