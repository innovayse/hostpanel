using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>What fraud detection made of one registration or order.</summary>
/// <param name="Checked">
/// <see langword="false"/> when fraud detection is switched off and nothing was looked at; the decision is then
/// <see cref="FraudDecision.Approved"/> and nothing is logged.
/// </param>
/// <param name="Decision">Approved, Review or Rejected.</param>
/// <param name="Score">The risk score, 0 to 100.</param>
/// <param name="Reasons">The reasons the score came from.</param>
public sealed record FraudVerdictDto(bool Checked, FraudDecision Decision, int Score, IReadOnlyList<FraudReasonCode> Reasons);
