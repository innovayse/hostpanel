namespace Innovayse.Hostpanel.Application.Services.Queries.GetSshAgentBundle;

/// <summary>
/// Anonymous: the SSH key agent installer bundle. It is the same public code for every server and
/// carries no secret; the installer command verifies its SHA256 against the value the administrator
/// copied from the authenticated admin panel.
/// </summary>
/// <param name="Profile">
/// The profile the bundle must install: the id a plugin names in its manifest, or the built in one. <see langword="null"/>
/// asks for the bundle with the built in profile only.
/// </param>
public sealed record GetSshAgentBundleQuery(string? Profile = null);
