using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetTldConfig;

/// <summary>
/// Handles <see cref="GetTldConfigQuery"/> by loading a single TLD configuration
/// and mapping it to a full <see cref="TldConfigDto"/> with all pricing dictionaries.
/// </summary>
public sealed class GetTldConfigHandler
{
    /// <summary>The data context the configuration is found in.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the configuration is found in.</param>
    public GetTldConfigHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the full admin DTO for a single TLD configuration.
    /// </summary>
    /// <param name="query">The query containing the TLD configuration ID to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with a <see cref="TldConfigDto"/> carrying every field and pricing dictionary, or
    /// <c>TLD_CONFIG_NOT_FOUND</c> (404) when no configuration with the given ID exists.
    /// </returns>
    public async Task<Result<TldConfigDto>> HandleAsync(GetTldConfigQuery query, CancellationToken ct)
    {
        var config = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Id == query.Id, ct);
        if (config is null)
        {
            return Result<TldConfigDto>.FailureCode(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
        }

        return Result.Success(MapToDto(config));
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
            new TldPriceTableDto(
                new Dictionary<int, decimal>(config.CostRegister),
                new Dictionary<int, decimal>(config.CostTransfer),
                new Dictionary<int, decimal>(config.CostRenew)),
            new TldPriceTableDto(
                new Dictionary<int, decimal>(config.SellRegister),
                new Dictionary<int, decimal>(config.SellTransfer),
                new Dictionary<int, decimal>(config.SellRenew)),
            config.LastSyncedAt,
            config.CreatedAt);
    }
}
