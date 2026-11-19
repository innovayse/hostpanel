namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>A website the host holds on a hosting service for a plugin.</summary>
/// <param name="Id">The host's id of the website.</param>
/// <param name="Hostname">The host name the website answers to.</param>
/// <param name="DocumentRoot">The document root, relative to the account's home directory.</param>
/// <param name="DocumentRootPath">The absolute path of the document root on the hosting server.</param>
/// <param name="PhpVersion">The PHP version it runs with, or <see langword="null"/> for none.</param>
public sealed record HostedWebsite(Guid Id, string Hostname, string DocumentRoot, string DocumentRootPath, string? PhpVersion);
