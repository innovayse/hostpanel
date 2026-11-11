namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// The running Hostpanel version, shown to every signed-in staff member. It carries nothing about
/// releases or upgrades, which stay behind the admin-only release status.
/// </summary>
/// <param name="InstalledVersion">The assembly informational version of the running build, e.g. <c>1.4.0</c>, or <c>dev</c> for a local build.</param>
public sealed record InstalledVersionDto(string InstalledVersion);
