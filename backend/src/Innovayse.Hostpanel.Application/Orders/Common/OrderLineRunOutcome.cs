using Innovayse.Hostpanel.Application.Orders.Common.Enums;

namespace Innovayse.Hostpanel.Application.Orders.Common;

/// <summary>What running one order line produced, before its outcome is recorded on the line.</summary>
/// <param name="Kind">Whether the line is a service or a domain line.</param>
/// <param name="DomainId">The new domain's id when the line registered or transferred one; otherwise <see langword="null"/>.</param>
/// <param name="ServiceId">The line's service when it created (or reused) one; otherwise <see langword="null"/>.</param>
/// <param name="ErrorCode">The code the refusing use case answered with, or <see langword="null"/> on success or when it gave none.</param>
/// <param name="FailureReason"><see langword="null"/> on success, otherwise a human-readable sentence for the refund and the log.</param>
internal sealed record OrderLineRunOutcome(
    OrderLineKind Kind,
    int? DomainId,
    int? ServiceId,
    string? ErrorCode,
    string? FailureReason)
{
    /// <summary>Gets whether the line could not be fulfilled.</summary>
    public bool Failed => FailureReason is not null;
}
