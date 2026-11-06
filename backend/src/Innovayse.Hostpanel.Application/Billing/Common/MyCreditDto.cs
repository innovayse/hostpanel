using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The client's own credit: the balance, the limits of one top-up and a page of the history, newest first.</summary>
/// <param name="Balance">The current credit balance.</param>
/// <param name="Currency">ISO 4217 code the balance and the top-up limits are in: the client's billing currency.</param>
/// <param name="TopUpMinimum">The least the client may add in one top-up.</param>
/// <param name="TopUpMaximum">The most the client may add in one top-up.</param>
/// <param name="Entries">A page of the credit history. Movements older than the history are not in it.</param>
public sealed record MyCreditDto(
    decimal Balance, string Currency, decimal TopUpMinimum, decimal TopUpMaximum, PaginatedList<MyCreditEntryDto> Entries);
