namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>What a plugin asks the host to set up as a website on a hosting service.</summary>
/// <param name="Hostname">The host name the website answers to.</param>
/// <param name="DocumentRoot">The document root, relative to the account's home directory.</param>
/// <param name="PhpVersion">The PHP version to run it with, or <see langword="null"/> for none.</param>
/// <param name="DocumentRootMayBeLink">Whether the plugin will later switch the document root through a symbolic link.</param>
public sealed record HostedWebsiteSpec(string Hostname, string DocumentRoot, string? PhpVersion, bool DocumentRootMayBeLink);
