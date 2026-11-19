using Innovayse.Hostpanel.Domain.Ssh;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// What a hosting provider is asked to set up for a website. Built by HostPanel from the service record and the
/// <see cref="ServiceWebsite"/> it recorded, never from anything a browser sent.
/// </summary>
/// <param name="Account">The account the website belongs to.</param>
/// <param name="WebsiteId">The website's stable identifier, so the provider can find it again.</param>
/// <param name="Hostname">The host name the website answers on.</param>
/// <param name="DocumentRoot">The directory, relative to the account's home, it serves files from.</param>
/// <param name="PhpVersion">The PHP version it runs, or <see langword="null"/> for none.</param>
/// <param name="DocumentRootMayBeLink">Whether the document root is a link the plugin switches on its deploys.</param>
public sealed record WebsiteOrder(
    SshAccount Account, Guid WebsiteId, string Hostname, string DocumentRoot, string? PhpVersion, bool DocumentRootMayBeLink);
