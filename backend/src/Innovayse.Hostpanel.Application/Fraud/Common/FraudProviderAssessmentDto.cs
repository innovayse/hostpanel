using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>What the external scoring service made of a request.</summary>
/// <param name="Score">The service's risk score, 0 to 100.</param>
/// <param name="Reasons">The reasons the service's answer adds.</param>
/// <param name="IpCountry">The country of the IP address (ISO 3166-1 alpha-2), or <see langword="null"/> when the service did not say.</param>
public sealed record FraudProviderAssessmentDto(int Score, IReadOnlyList<FraudReasonCode> Reasons, string? IpCountry);
