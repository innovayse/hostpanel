namespace Innovayse.Hostpanel.Infrastructure.Integrations.MaxMind;

/// <summary>The part of a minFraud Insights answer the host reads. Members the host does not use are left out.</summary>
/// <param name="RiskScore">The service's risk score for the request, 0.01 to 99 (a higher score is riskier).</param>
/// <param name="IpAddress">What the service knows about the caller's IP address.</param>
internal sealed record MaxMindInsightsWire(double? RiskScore, MaxMindIpAddressWire? IpAddress);
