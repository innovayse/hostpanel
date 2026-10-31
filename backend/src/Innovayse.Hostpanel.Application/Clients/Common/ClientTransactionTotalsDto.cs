using Innovayse.Hostpanel.Application.Billing.Currencies.Common;

namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>What a client's transactions add up to, one entry per currency the client has transactions in.</summary>
/// <param name="GrossRevenue">Total income from transactions, per currency.</param>
/// <param name="ClientExpenses">Total outgoing from transactions, per currency.</param>
/// <param name="TotalFees">Total fees from transactions, per currency.</param>
/// <param name="NetIncome">Computed per currency: GrossRevenue - ClientExpenses - TotalFees.</param>
public sealed record ClientTransactionTotalsDto(
    IReadOnlyList<CurrencyAmountDto> GrossRevenue,
    IReadOnlyList<CurrencyAmountDto> ClientExpenses,
    IReadOnlyList<CurrencyAmountDto> TotalFees,
    IReadOnlyList<CurrencyAmountDto> NetIncome);
