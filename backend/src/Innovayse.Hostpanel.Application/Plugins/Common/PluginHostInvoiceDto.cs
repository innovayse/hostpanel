using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The columns of one client-visible invoice the plugin host API shares; no notes, lines or payment details.</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="Status">The invoice status, never a draft; mapped to the SDK's by name before it reaches a plugin.</param>
/// <param name="Total">The invoice total, in <paramref name="Currency"/>.</param>
/// <param name="Currency">ISO 4217 code the invoice is billed in.</param>
/// <param name="InvoiceDate">The date the invoice was issued (UTC).</param>
/// <param name="DueDate">The date payment is due (UTC).</param>
/// <param name="PaidAt">When the invoice was paid (UTC), or <see langword="null"/> while it is not.</param>
public sealed record PluginHostInvoiceDto(
    int Id,
    InvoiceStatus Status,
    decimal Total,
    string Currency,
    DateTimeOffset InvoiceDate,
    DateTimeOffset DueDate,
    DateTimeOffset? PaidAt);
