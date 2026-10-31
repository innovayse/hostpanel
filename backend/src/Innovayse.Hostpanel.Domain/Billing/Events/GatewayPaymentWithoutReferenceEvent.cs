using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing.Events;

/// <summary>
/// A hosted gateway reported a payment as paid, but neither its status nor the invoice carries a
/// reference to record it under. Nothing is recorded or credited, because a replay could not tell the
/// payment from another, so the money taken at the gateway waits for an operator. The event is
/// published once per session, after the save that marks it, on an open and on an already settled invoice.
/// </summary>
/// <param name="InvoiceId">The invoice the session was opened for.</param>
/// <param name="ClientId">The client the invoice belongs to.</param>
/// <param name="Module">The payment plugin id that took the money.</param>
/// <param name="Amount">The amount the gateway reports as taken, in <paramref name="Currency"/>; <see langword="null"/> when it reports none.</param>
/// <param name="Currency">ISO 4217 code of the invoice.</param>
public sealed record GatewayPaymentWithoutReferenceEvent(
    int InvoiceId, int ClientId, string Module, decimal? Amount, string Currency) : IDomainEvent
{
    /// <summary>When the change was made, UTC, given by whoever raised the event.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
