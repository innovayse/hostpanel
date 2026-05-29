namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Application.Common;

/// <summary>
/// Wraps a paginated list of transactions with financial summary totals.
/// </summary>
/// <param name="Transactions">Paginated transaction list.</param>
/// <param name="TotalIn">Sum of all AmountIn across all transactions for the client.</param>
/// <param name="TotalOut">Sum of all AmountOut across all transactions for the client.</param>
/// <param name="TotalFees">Sum of all Fees across all transactions for the client.</param>
/// <param name="Balance">Net balance: TotalIn − TotalOut − TotalFees.</param>
public record TransactionsResultDto(
    PagedResult<TransactionDto> Transactions,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalFees,
    decimal Balance);
