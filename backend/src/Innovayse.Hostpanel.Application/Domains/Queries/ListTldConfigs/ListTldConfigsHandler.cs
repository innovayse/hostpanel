using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ConvertAmounts;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListTldConfigs;

/// <summary>
/// Handles <see cref="ListTldConfigsQuery"/> by loading one page of TLD configurations, narrowed by the search text
/// on the TLD and by registrar module and enabled state in SQL ahead of paging, and mapping them to
/// <see cref="TldConfigListItemDto"/> summaries with margin calculations.
/// </summary>
public sealed class ListTldConfigsHandler
{
    /// <summary>The data context every TLD configuration is listed from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to convert each cost price into its sell currency through the billing currencies.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context every TLD configuration is listed from.</param>
    /// <param name="bus">Wolverine bus, used to convert each cost price into its sell currency through the billing currencies.</param>
    public ListTldConfigsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>
    /// Returns a page of TLD configurations as admin list-item DTOs with calculated margin percentages.
    /// </summary>
    /// <param name="query">The page, page size, search and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with a page of configurations ordered by sort order, then TLD, mapped to
    /// <see cref="TldConfigListItemDto"/>. This use case answers no refusal code of its own.
    /// </returns>
    public async Task<Result<PaginatedList<TldConfigListItemDto>>> HandleAsync(ListTldConfigsQuery query, CancellationToken ct)
    {
        // A TLD is stored without its dot, so ".com" typed into the search must find "com".
        var filtered = _context.TldConfigs.AsNoTracking().Search(query.Search?.Trim().TrimStart('.'), x => x.Tld);

        if (query.RegistrarModule is { } registrarModule)
        {
            filtered = filtered.Where(x => x.RegistrarModule == registrarModule);
        }

        if (query.Enabled is { } enabled)
        {
            filtered = filtered.Where(x => x.IsEnabled == enabled);
        }

        // The id ends the order so a page never repeats or skips a row between requests.
        var ordered = filtered.OrderBy(x => x.SortOrder).ThenBy(x => x.Tld).ThenBy(x => x.Id);
        var totalCount = await ordered.CountAsync(ct);
        var configs = await ordered.Paginate(query.Page, query.PageSize).ToListAsync(ct);
        var costsInSellCurrency = await ConvertCostsToSellCurrencyAsync(configs, ct);

        return Result.Success(new PaginatedList<TldConfigListItemDto>(
            configs.Select(config => MapToListItem(config, costsInSellCurrency.GetValueOrDefault(config.Id))).ToList(),
            totalCount,
            query.Page,
            query.PageSize));
    }

    /// <summary>
    /// Converts each configuration's 1-year registration cost, stated in its cost currency, into its sell currency
    /// through the stored rates, so a margin compares two prices in one currency. One conversion is asked per sell
    /// currency, through the billing module.
    /// </summary>
    /// <param name="configs">The configurations of the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The converted cost keyed by configuration id. A configuration with no 1-year cost, a cost currency that has no
    /// stored rate, or a sell currency that is not configured has no entry: its margin cannot be told.
    /// </returns>
    private async Task<Dictionary<int, decimal>> ConvertCostsToSellCurrencyAsync(
        IReadOnlyList<TldConfig> configs, CancellationToken ct)
    {
        var converted = new Dictionary<int, decimal>();
        var withCost = configs.Where(config => config.CostRegister.ContainsKey(1)).ToList();
        foreach (var group in withCost.GroupBy(config => config.SellCurrency))
        {
            var members = group.ToList();
            var amounts = members.Select(config => new CurrencyAmountDto(config.CostRegister[1], config.Currency)).ToList();
            var answer = await _bus.InvokeAsync<Result<ConvertedAmountsDto>>(new ConvertAmountsQuery(group.Key, amounts), ct);
            if (answer.IsFailure)
            {
                continue;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var amount = answer.Value!.Amounts[i];
                if (amount.Converted)
                {
                    converted[members[i].Id] = amount.Amount;
                }
            }
        }

        return converted;
    }

    /// <summary>
    /// Maps a <see cref="TldConfig"/> entity to a <see cref="TldConfigListItemDto"/>,
    /// calculating the margin percentage from the 1-year registration prices.
    /// </summary>
    /// <param name="config">The TLD configuration entity to map.</param>
    /// <param name="costInSellCurrency">The 1-year cost converted into the sell currency, or null when it could not be.</param>
    /// <returns>A summary DTO with 1-year prices and margin percentage.</returns>
    private static TldConfigListItemDto MapToListItem(TldConfig config, decimal? costInSellCurrency)
    {
        var cost1yr = config.CostRegister.TryGetValue(1, out var c) ? c : (decimal?)null;
        var sell1yr = config.SellRegister.TryGetValue(1, out var s) ? s : (decimal?)null;

        // Cost and sell are stated in different currencies, so the margin is worked out on the converted cost.
        decimal? marginPercent = null;
        if (costInSellCurrency is > 0 && sell1yr.HasValue)
        {
            marginPercent = Math.Round((sell1yr.Value - costInSellCurrency.Value) / costInSellCurrency.Value * 100, 2);
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
