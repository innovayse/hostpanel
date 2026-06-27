namespace Innovayse.Application.Domains.Commands.SyncTldCostPrices;

/// <summary>
/// Scheduled command that refreshes registrar cost prices for all TLD configurations
/// by polling each registrar provider and re-schedules itself for the next day at 04:00 UTC.
/// </summary>
public record SyncTldCostPricesCommand;
