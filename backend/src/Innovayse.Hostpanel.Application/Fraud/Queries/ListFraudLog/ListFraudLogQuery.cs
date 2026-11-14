using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;

/// <summary>Lists the fraud log, newest first, optionally searched and narrowed.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only entries whose e-mail address or IP address contains it (case-insensitive) are listed. Blank lists all.
/// </param>
/// <param name="Kind">Optional filter by what was checked; an unknown value is refused, never ignored.</param>
/// <param name="Decision">Optional filter by decision; an unknown value is refused, never ignored.</param>
/// <param name="Reason">Optional filter by a reason the score came from; an unknown value is refused, never ignored.</param>
public sealed record ListFraudLogQuery(
    int Page,
    int PageSize,
    string? Search = null,
    FraudCheckKind? Kind = null,
    FraudDecision? Decision = null,
    FraudReasonCode? Reason = null) : IListQuery;
