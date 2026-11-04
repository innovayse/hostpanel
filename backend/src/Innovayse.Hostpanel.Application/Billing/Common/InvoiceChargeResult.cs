using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>The answer to charging an invoice's saved method.</summary>
/// <param name="Outcome">How the charge ended.</param>
/// <param name="ErrorCode">The error code to report, set when <paramref name="Outcome"/> is <see cref="InvoiceChargeOutcome.NotRecorded"/>.</param>
public sealed record InvoiceChargeResult(InvoiceChargeOutcome Outcome, string? ErrorCode = null);
