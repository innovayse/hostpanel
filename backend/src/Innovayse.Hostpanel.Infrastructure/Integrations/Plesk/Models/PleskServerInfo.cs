namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>What a connection test reports about a Plesk server.</summary>
/// <param name="CustomerCount">How many customers the server holds.</param>
/// <param name="Version">The Plesk version, e.g. <c>18.0.82</c>, or null when not reported.</param>
public sealed record PleskServerInfo(int CustomerCount, string? Version);
