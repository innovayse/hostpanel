namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;

/// <summary>What a connection test reads from WHM.</summary>
/// <param name="Version">The WHM version, e.g. <c>11.126.0.5</c>, or null when WHM did not say.</param>
public sealed record WhmServerInfo(string? Version);
