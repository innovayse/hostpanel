using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;

/// <summary>The invoice as the database holds it after a conflicting save.</summary>
/// <param name="Replay">The answer when the payment is already on the ledger (a concurrent request recorded it); <see langword="null"/> otherwise.</param>
/// <param name="Invoice">The freshly loaded invoice; <see langword="null"/> when the payment is recorded or when the invoice no longer exists.</param>
internal sealed record SettleReload(Result? Replay, Invoice? Invoice);
