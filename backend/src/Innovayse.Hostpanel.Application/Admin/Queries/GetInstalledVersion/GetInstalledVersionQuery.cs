namespace Innovayse.Hostpanel.Application.Admin.Queries.GetInstalledVersion;

/// <summary>
/// Returns the version of the running Hostpanel build. It takes no parameters and reads nothing
/// but <c>IInstalledVersionProvider</c>, so any signed-in staff role may ask.
/// </summary>
public sealed record GetInstalledVersionQuery;
