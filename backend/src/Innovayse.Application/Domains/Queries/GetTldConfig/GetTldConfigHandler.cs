namespace Innovayse.Application.Domains.Queries.GetTldConfig;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Handles <see cref="GetTldConfigQuery"/> by loading a single TLD configuration
/// and mapping it to a full <see cref="TldConfigDto"/> with all pricing dictionaries.
/// </summary>
/// <param name="repo">TLD configuration repository for finding a config by ID.</param>
public sealed class GetTldConfigHandler(ITldConfigRepository repo)
{
    /// <summary>
    /// Returns the full admin DTO for a single TLD configuration, or null if not found.
    /// </summary>
    /// <param name="query">The query containing the TLD configuration ID to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="TldConfigDto"/> with all fields and pricing dictionaries, or
    /// <see langword="null"/> if no configuration with the given ID exists.
    /// </returns>
    public async Task<TldConfigDto?> HandleAsync(GetTldConfigQuery query, CancellationToken ct)
    {
        var config = await repo.FindByIdAsync(query.Id, ct);
        if (config is null)
        {
            return null;
        }

        return MapToDto(config);
    }

    /// <summary>
    /// Maps a <see cref="TldConfig"/> entity to a full <see cref="TldConfigDto"/>,
    /// copying all six pricing dictionaries into new instances.
    /// </summary>
    /// <param name="config">The TLD configuration entity to map.</param>
    /// <returns>A fully populated <see cref="TldConfigDto"/>.</returns>
    private static TldConfigDto MapToDto(TldConfig config)
    {
        return new TldConfigDto(
            config.Id,
            config.Tld,
            config.RegistrarModule.ToString(),
            config.Currency,
            config.SellCurrency,
            config.IsEnabled,
            config.SortOrder,
            config.Categories.ToList(),
            new Dictionary<int, decimal>(config.CostRegister),
            new Dictionary<int, decimal>(config.CostTransfer),
            new Dictionary<int, decimal>(config.CostRenew),
            new Dictionary<int, decimal>(config.SellRegister),
            new Dictionary<int, decimal>(config.SellTransfer),
            new Dictionary<int, decimal>(config.SellRenew),
            config.LastSyncedAt,
            config.CreatedAt);
    }
}
