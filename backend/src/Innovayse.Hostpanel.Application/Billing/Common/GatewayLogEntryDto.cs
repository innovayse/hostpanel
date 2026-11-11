using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One entry of the payment-gateway log.</summary>
/// <param name="Id">Entry key, unique across both sources: <c>tx-{id}</c> for a recorded transaction, <c>session-{invoiceId}</c> for an open gateway session.</param>
/// <param name="DateTime">UTC time of the interaction.</param>
/// <param name="Gateway">Gateway or payment plugin name.</param>
/// <param name="DebugData">Invoice, gateway reference and notes, as text.</param>
/// <param name="Result">How the interaction ended.</param>
/// <param name="InvoiceId">The invoice the interaction belongs to, when it has one.</param>
public sealed record GatewayLogEntryDto(
    string Id,
    DateTimeOffset DateTime,
    string Gateway,
    string DebugData,
    GatewayLogResult Result,
    int? InvoiceId);
