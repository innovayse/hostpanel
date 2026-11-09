namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.InspectPluginPackage;

/// <summary>
/// Reads what an uploaded plugin ZIP asks for (host access, capabilities, changelog and supported host versions) before
/// the admin confirms the install. Nothing is unpacked or installed.
/// </summary>
/// <param name="Content">
/// Stream of the uploaded ZIP file, or <see langword="null"/> when the request carried no file; the caller owns and
/// disposes it.
/// </param>
public sealed record InspectPluginPackageQuery(Stream? Content);
