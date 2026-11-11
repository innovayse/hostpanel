using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>What a client's plan change request came to once it was made.</summary>
/// <param name="RequestId">The plan change request.</param>
/// <param name="Type">Whether the move is an upgrade or a downgrade. Sent as the member name.</param>
/// <param name="Status">
/// Where the request stands: <c>Completed</c> when the service already runs on the new plan, <c>Approved</c> for an
/// upgrade waiting for its invoice to be paid, <c>Pending</c> for a downgrade waiting for staff. Sent as the member name.
/// </param>
/// <param name="ProrationAmount">The prorated difference, in <paramref name="CurrencyCode"/>.</param>
/// <param name="CurrencyCode">ISO 4217 alpha code of the client's billing currency.</param>
/// <param name="InvoiceId">The proration invoice the client pays for an upgrade, or <see langword="null"/> when there is none.</param>
public sealed record ServicePlanChangeResultDto(
    int RequestId,
    ServiceChangeType Type,
    ServiceChangeStatus Status,
    decimal ProrationAmount,
    string CurrencyCode,
    int? InvoiceId);
