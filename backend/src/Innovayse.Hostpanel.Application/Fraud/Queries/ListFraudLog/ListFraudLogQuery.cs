using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;

/// <summary>Lists the fraud log, newest first.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Decision">Optional filter by decision; an unknown value is refused when the query is bound, never ignored.</param>
/// <param name="Reason">Optional filter by a reason the score came from; an unknown value is refused when the query is bound.</param>
public sealed record ListFraudLogQuery(int Page, int PageSize, FraudDecision? Decision, FraudReasonCode? Reason);
