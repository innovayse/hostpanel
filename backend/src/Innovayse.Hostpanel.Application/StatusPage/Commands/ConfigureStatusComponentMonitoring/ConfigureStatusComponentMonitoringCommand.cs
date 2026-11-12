namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ConfigureStatusComponentMonitoring;

/// <summary>Command to set how a status page component is monitored automatically.</summary>
/// <param name="Id">
/// The component identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="CheckUrl">The absolute http or https address to request; empty when the component has none.</param>
/// <param name="Enabled">Whether the automatic check runs; needs an address.</param>
/// <param name="FailureThreshold">How many failed checks in a row make the component count as down, from 1 to 10.</param>
public sealed record ConfigureStatusComponentMonitoringCommand(int Id, string CheckUrl, bool Enabled, int FailureThreshold);
