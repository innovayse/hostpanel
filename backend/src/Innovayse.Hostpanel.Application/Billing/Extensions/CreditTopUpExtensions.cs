using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Puts the amount of a paid credit top-up invoice on the client's balance, so every path that can settle an invoice
/// (gateway completion, a saved card, a confirmed bank transfer, a recorded payment, a bulk mark-paid) credits a top-up
/// the same way and none can forget to.
/// </summary>
/// <remarks>
/// The credit is staged on the caller's unit of work, in the same save that turns the invoice Paid. That save is guarded
/// by the invoice row's concurrency token and a paid invoice refuses a second payment, so of any number of paths and
/// replays racing on one top-up exactly one save lands and the balance rises once. A path that lost the race stages
/// nothing that is saved.
/// </remarks>
public static class CreditTopUpExtensions
{
    /// <summary>
    /// Stages the credit of a top-up invoice that was just settled. Does nothing for an invoice that is not a top-up or is
    /// not paid, so a caller may call it after every settlement without a check of its own.
    /// </summary>
    /// <param name="clientCredit">The client credit port, staging on the caller's unit of work.</param>
    /// <param name="invoice">The invoice that was just settled (tracked).</param>
    /// <param name="payment">
    /// The ledger entry of the payment that completed the invoice; it is stamped as the top-up, so the money and the credit
    /// are one line. <see langword="null"/> when no entry was written.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CLIENT_NOT_FOUND</c> when the invoice's client is gone; the caller must not save then.
    /// </returns>
    public static async Task<Result> CreditPaidTopUpAsync(
        this IClientCredit clientCredit, Invoice invoice, Transaction? payment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clientCredit);
        ArgumentNullException.ThrowIfNull(invoice);

        if (!invoice.IsCreditTopUp || invoice.Status != InvoiceStatus.Paid)
        {
            return Result.Success();
        }

        var credited = await clientCredit.AddAsync(
            new ClientCreditChange(invoice.ClientId, invoice.TopUpAmount, CreditMovementType.TopUp, invoice.Id, Entry: payment), ct);
        return credited.IsFailure ? Result.FailureCode(credited.ErrorCode!) : Result.Success();
    }
}
