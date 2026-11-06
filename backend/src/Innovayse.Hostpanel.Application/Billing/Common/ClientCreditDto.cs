using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A client's credit as staff see it: the balance and a page of its history, newest first.</summary>
/// <param name="ClientId">The client.</param>
/// <param name="Balance">The current credit balance.</param>
/// <param name="Currency">ISO 4217 code the balance is held in.</param>
/// <param name="Entries">A page of the credit history. Movements older than the history are not in it.</param>
public sealed record ClientCreditDto(int ClientId, decimal Balance, string Currency, PaginatedList<CreditEntryDto> Entries);
