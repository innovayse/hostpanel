using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;

/// <summary>
/// Handles <see cref="ListTldConfigsQuery"/> by loading all TLD configurations
/// and mapping them to <see cref="TldConfigListItemDto"/> summaries with margin calculations.
/// </summary>
public sealed class ListTldConfigsHandler
{
    /// <summary>The data context every TLD configuration is listed from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context every TLD configuration is listed from.</param>
    public ListTldConfigsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all TLD configurations as admin list-item DTOs with calculated margin percentages.
    /// </summary>
    /// <param name="query">The list TLD configs query (carries no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with every TLD configuration ordered by sort order, mapped to
    /// <see cref="TldConfigListItemDto"/>. This use case answers no refusal code of its own.
    /// </returns>
    public async Task<Result<List<TldConfigListItemDto>>> HandleAsync(ListTldConfigsQuery query, CancellationToken ct)
    {
        var configs = await _context.TldConfigs.OrderBy(x => x.SortOrder).ThenBy(x => x.Tld).ToListAsync(ct);

        return Result.Success(configs.Select(MapToListItem).ToList());
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
