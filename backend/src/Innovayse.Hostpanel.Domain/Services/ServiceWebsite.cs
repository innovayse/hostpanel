using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>One website a plugin set up for a client on a hosting service's server.</summary>
public sealed class ServiceWebsite : HostingResource
{
    /// <summary>
    /// Gets the stable public identifier of this website. The provider is addressed by it and the API routes use it:
    /// the sequential database id is never exposed.
    /// </summary>
    public Guid WebsiteId { get; private set; }

    /// <summary>Gets the host name the website answers on, lower case.</summary>
    public string Hostname { get; private set; } = string.Empty;

    /// <summary>Gets the directory, relative to the account's home, the website serves files from.</summary>
    public string DocumentRoot { get; private set; } = string.Empty;

    /// <summary>Gets the PHP version the website runs, or <see langword="null"/> when it runs none.</summary>
    public string? PhpVersion { get; private set; }

    /// <summary>
    /// Gets the absolute path of the PHP command line binary that matches <see cref="PhpVersion"/> on the server, or
    /// <see langword="null"/> when the website runs no PHP, the server did not report the path, or the website was set
    /// up before the host kept it.
    /// </summary>
    public string? PhpCliPath { get; private set; }

    /// <summary>
    /// Gets whether the website runs PHP but its CLI path is not known yet, so it is asked of the server once more the
    /// next time the website is ensured.
    /// </summary>
    public bool NeedsPhpCliPath => PhpVersion is not null && PhpCliPath is null;

    /// <summary>
    /// Gets whether the document root is a symbolic link the plugin switches on its deploys, so the server applies the
    /// settings that allow a link as a document root.
    /// </summary>
    public bool DocumentRootMayBeLink { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ServiceWebsite()
    {
    }

    /// <summary>Builds the pending website once <see cref="Create"/> has checked the texts.</summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The pinned server.</param>
    /// <param name="accountUsername">The pinned account.</param>
    /// <param name="issuedToPluginId">The plugin it is issued to.</param>
    /// <param name="hostname">The host name.</param>
    /// <param name="documentRoot">The document root.</param>
    /// <param name="phpVersion">The PHP version, if any.</param>
    /// <param name="documentRootMayBeLink">Whether the document root is a link the plugin switches.</param>
    private ServiceWebsite(
        int clientServiceId, int serverId, string accountUsername, string issuedToPluginId,
        string hostname, string documentRoot, string? phpVersion, bool documentRootMayBeLink)
        : base(clientServiceId, serverId, accountUsername, issuedToPluginId)
    {
        WebsiteId = Guid.NewGuid();
        Hostname = hostname.Trim().ToLowerInvariant();
        DocumentRoot = documentRoot.Trim();
        PhpVersion = phpVersion?.Trim();
        DocumentRootMayBeLink = documentRootMayBeLink;
    }

    /// <summary>Records the website being created.</summary>
    /// <param name="clientServiceId">The owning service.</param>
    /// <param name="serverId">The server the service is assigned to right now.</param>
    /// <param name="accountUsername">The Linux account on that server right now.</param>
    /// <param name="issuedToPluginId">The plugin the host issues the website to.</param>
    /// <param name="hostname">The host name; trimmed and lower-cased.</param>
    /// <param name="documentRoot">The directory it serves files from.</param>
    /// <param name="phpVersion">The PHP version, or <see langword="null"/> for none.</param>
    /// <param name="documentRootMayBeLink">Whether the document root is a link the plugin switches on its deploys.</param>
    /// <returns>The new, pending website.</returns>
    /// <exception cref="ArgumentException">A required text is blank, or <paramref name="phpVersion"/> is set and blank.</exception>
    public static ServiceWebsite Create(
        int clientServiceId, int serverId, string accountUsername, string issuedToPluginId,
        string hostname, string documentRoot, string? phpVersion, bool documentRootMayBeLink)
    {
        EnsureText(hostname, nameof(hostname));
        EnsureText(documentRoot, nameof(documentRoot));
        if (phpVersion is not null)
        {
            EnsureText(phpVersion, nameof(phpVersion));
        }

        return new ServiceWebsite(
            clientServiceId, serverId, accountUsername, issuedToPluginId, hostname, documentRoot, phpVersion, documentRootMayBeLink);
    }

    /// <summary>
    /// Whether the website is already as the specification says, so asking for it again needs no call to the server.
    /// The host name is the website's identity and is not compared.
    /// </summary>
    /// <param name="documentRoot">The document root asked for.</param>
    /// <param name="phpVersion">The PHP version asked for, or <see langword="null"/> for none.</param>
    /// <param name="documentRootMayBeLink">Whether the document root may be a link.</param>
    /// <returns><see langword="true"/> when all three equal what the website holds.</returns>
    public bool Matches(string documentRoot, string? phpVersion, bool documentRootMayBeLink)
    {
        return string.Equals(DocumentRoot, documentRoot.Trim(), StringComparison.Ordinal)
            && string.Equals(PhpVersion, phpVersion?.Trim(), StringComparison.Ordinal)
            && DocumentRootMayBeLink == documentRootMayBeLink;
    }

    /// <summary>Records the specification the server confirmed it applied.</summary>
    /// <param name="documentRoot">The directory it serves files from.</param>
    /// <param name="phpVersion">The PHP version, or <see langword="null"/> for none.</param>
    /// <param name="documentRootMayBeLink">Whether the document root is a link the plugin switches on its deploys.</param>
    /// <param name="phpCliPath">
    /// The PHP CLI path the server reported for <paramref name="phpVersion"/>, or <see langword="null"/> when it reported
    /// none; ignored, and so cleared, when the website runs no PHP.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The document root is blank, or <paramref name="phpVersion"/> or <paramref name="phpCliPath"/> is set and blank.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the website is not active.</exception>
    public void Respecify(string documentRoot, string? phpVersion, bool documentRootMayBeLink, string? phpCliPath)
    {
        EnsureText(documentRoot, nameof(documentRoot));
        if (phpVersion is not null)
        {
            EnsureText(phpVersion, nameof(phpVersion));
        }

        if (phpCliPath is not null)
        {
            EnsureText(phpCliPath, nameof(phpCliPath));
        }

        if (State is not HostingResourceState.Active)
        {
            throw new InvalidOperationException("Only an active website can change its specification.");
        }

        DocumentRoot = documentRoot.Trim();
        PhpVersion = phpVersion?.Trim();
        PhpCliPath = phpVersion is null ? null : phpCliPath?.Trim();
        DocumentRootMayBeLink = documentRootMayBeLink;
    }
}
