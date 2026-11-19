namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>Whose budget an access is counted against: one plugin, one client, one named budget.</summary>
/// <param name="PluginId">The plugin asking.</param>
/// <param name="ClientId">The client the call acts for.</param>
/// <param name="Budget">The rate limit tier the access is counted in, for example the portal's <c>ssh-keys</c>.</param>
public sealed record PluginHostAccessKey(string PluginId, int ClientId, string Budget);
