namespace Innovayse.Application.Domains.Queries.ListTldConfigs;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Handles <see cref="ListTldConfigsQuery"/> by loading all TLD configurations
/// and mapping them to <see cref="TldConfigListItemDto"/> summaries with margin calculations.
/// </summary>
/// <param name="repo">TLD configuration repository for listing all configs.</param>
public sealed class ListTldConfigsHandler(ITldConfigRepository repo)
{
    /// <summary>
    /// Returns all TLD configurations as admin list-item DTOs with calculated margin percentages.
    /// </summary>
    /// <param name="query">The list TLD configs query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All TLD configurations ordered by sort order, mapped to <see cref="TldConfigListItemDto"/>.</returns>
    public async Task<List<TldConfigListItemDto>> HandleAsync(ListTldConfigsQuery query, CancellationToken ct)
    {
        var configs = await repo.ListAllAsync(ct);

        return configs.Select(MapToListItem).ToList();
    }

    /// <summary>
    /// Maps a <see cref="TldConfig"/> entity to a <see cref="TldConfigListItemDto"/>,
    /// calculating the margin percentage from the 1-year registration prices.
    /// </summary>
    /// <param name="config">The TLD configuration entity to map.</param>
    /// <returns>A summary DTO with 1-year prices and margin percentage.</returns>
    private static TldConfigListItemDto MapToListItem(TldConfig config)
    {
        var cost1yr = config.CostRegister.TryGetValue(1, out var c) ? c : (decimal?)null;
        var sell1yr = config.SellRegister.TryGetValue(1, out var s) ? s : (decimal?)null;

        decimal? marginPercent = null;
        if (cost1yr.HasValue && cost1yr.Value > 0 && sell1yr.HasValue)
        {
            marginPercent = Math.Round((sell1yr.Value - cost1yr.Value) / cost1yr.Value * 100, 2);
        }

        return new TldConfigListItemDto(
            config.Id,
            config.Tld,
            config.RegistrarModule.ToString(),
            config.IsEnabled,
            cost1yr,
            sell1yr,
            marginPercent,
            config.Currency,
            config.SellCurrency,
            config.Categories.ToList(),
            config.LastSyncedAt,
            config.SortOrder);
    }
}
