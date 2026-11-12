using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>One row of the fraud log.</summary>
/// <param name="Id">Log entry primary key.</param>
/// <param name="Kind">What was checked.</param>
/// <param name="Decision">What was decided.</param>
/// <param name="ClientId">The client, when there is one.</param>
/// <param name="OrderId">The order, when there is one.</param>
/// <param name="IpAddress">The caller's IP address, when it was known.</param>
/// <param name="Email">The e-mail address checked, when it was known.</param>
/// <param name="Score">The risk score, 0 to 100.</param>
/// <param name="Reasons">The reasons the score came from.</param>
/// <param name="CreatedAt">When the decision was made (UTC).</param>
public sealed record FraudLogEntryDto(
    int Id,
    FraudCheckKind Kind,
    FraudDecision Decision,
    int? ClientId,
    int? OrderId,
    string? IpAddress,
    string? Email,
    int Score,
    IReadOnlyList<FraudReasonCode> Reasons,
    DateTimeOffset CreatedAt);
