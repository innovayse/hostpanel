namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>What one request to a component's check address found.</summary>
/// <param name="Succeeded"><see langword="true"/> when the address answered with a success status in time.</param>
/// <param name="LatencyMs">How long the request took, in milliseconds.</param>
public sealed record StatusProbeResult(bool Succeeded, int LatencyMs);
