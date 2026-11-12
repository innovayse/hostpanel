using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;

/// <summary>
/// Writes one decision to the fraud log. Dispatched by the Orders and Auth use cases that made it, never bound from a request: the
/// IP address and e-mail are the ones the deciding use case saw (for a staff decision, the order's own), not the caller's.
/// </summary>
/// <param name="Kind">What was checked.</param>
/// <param name="Decision">What was decided.</param>
/// <param name="ClientId">The client, when there is one.</param>
/// <param name="OrderId">The order, when there is one.</param>
/// <param name="IpAddress">The IP address the request came from, when known.</param>
/// <param name="Email">The e-mail address checked, when known.</param>
/// <param name="Score">The risk score, 0 to 100.</param>
/// <param name="Reasons">The reasons the score came from.</param>
public sealed record RecordFraudDecisionCommand(
    FraudCheckKind Kind,
    FraudDecision Decision,
    int? ClientId,
    int? OrderId,
    string? IpAddress,
    string? Email,
    int Score,
    IReadOnlyList<FraudReasonCode> Reasons);
