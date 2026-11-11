using Innovayse.Hostpanel.Application.Orders.Common.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>One order line that fulfilment could not complete.</summary>
/// <param name="ItemId">The order line.</param>
/// <param name="Kind">Whether the line is a service or a domain line.</param>
/// <param name="ProductName">The product name the line was ordered under.</param>
/// <param name="Domain">The domain the line names, or <see langword="null"/> when it names none.</param>
/// <param name="FirstPaymentAmount">The line's first payment, before tax, in the order currency.</param>
/// <param name="ErrorCode">The code the refusing use case answered with, or <see langword="null"/> when it gave none.</param>
/// <param name="Reason">A human-readable sentence for the log, the refund note and the admin.</param>
/// <param name="UnrecordedAttempt">
/// The attempt that still holds the line because its failure could not be recorded, or <see langword="null"/> when the
/// failure is on record. The automatic refund of a refused domain passes it on, so it runs all the same.
/// </param>
public sealed record OrderLineFailureDto(
    int ItemId,
    OrderLineKind Kind,
    string ProductName,
    string? Domain,
    decimal FirstPaymentAmount,
    string? ErrorCode,
    string Reason,
    int? UnrecordedAttempt = null);
