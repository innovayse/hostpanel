using System.Text.RegularExpressions;

/*
 * The shapes a plugin's website or database request must have (#1294). They mirror what the hosting agent accepts
 * (backend/agent/ssh-key-agent: DOMAIN_RE, DOCROOT_RE, LABEL_RE, PHP_VERSION_RE), so a request the agent would refuse is
 * refused here first, before the plugin's budget is spent and before any server is called. The agent still checks
 * everything again: this is the early answer, not the authority.
 */

namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>
/// Checks the host name, document root, PHP version and database label of a plugin's hosting request, and builds the
/// absolute document root path a website is reported with. Pure text rules: nothing here reads a server or a setting.
/// </summary>
public static partial class HostingSpecRules
{
    /// <summary>The directory every hosting account's home sits in on the managed servers.</summary>
    public const string HomeRoot = "/home";

    /// <summary>The host a database user is allowed to connect from, so the host a plugin connects to: the server itself.</summary>
    public const string DatabaseHost = "127.0.0.1";

    /// <summary>The port the database server listens on.</summary>
    public const int DatabasePort = 3306;

    /// <summary>The longest document root, in characters, the agent accepts.</summary>
    private const int MaxDocumentRootLength = 200;

    /// <summary>Whether <paramref name="hostname"/> is a lower case domain name, the pattern the agent checks.</summary>
    /// <param name="hostname">The host name, already trimmed and lower-cased.</param>
    /// <returns><see langword="true"/> when the shape is valid.</returns>
    public static bool IsHostname(string hostname)
    {
        return HostnamePattern().IsMatch(hostname);
    }

    /// <summary>Whether the host name is the service's own domain or a subdomain of it, the only names the host sets up.</summary>
    /// <param name="hostname">The host name, already trimmed and lower-cased.</param>
    /// <param name="serviceDomain">The domain of the service, or <see langword="null"/> when it has none.</param>
    /// <returns><see langword="true"/> when the host name is the domain or ends with a dot and the domain.</returns>
    public static bool IsHostnameOfDomain(string hostname, string? serviceDomain)
    {
        if (string.IsNullOrWhiteSpace(serviceDomain))
        {
            return false;
        }

        var domain = serviceDomain.Trim().ToLowerInvariant();
        return hostname == domain || hostname.EndsWith("." + domain, StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="documentRoot"/> is a relative path of one to six plain segments inside the home.</summary>
    /// <param name="documentRoot">The document root, already trimmed.</param>
    /// <returns><see langword="true"/> when the agent would accept it.</returns>
    public static bool IsDocumentRoot(string documentRoot)
    {
        return documentRoot.Length <= MaxDocumentRootLength && DocumentRootPattern().IsMatch(documentRoot);
    }

    /// <summary>Whether <paramref name="phpVersion"/> is written as <c>N.N</c> or <c>N.NN</c>.</summary>
    /// <param name="phpVersion">The PHP version, already trimmed.</param>
    /// <returns><see langword="true"/> when the shape is valid.</returns>
    public static bool IsPhpVersion(string phpVersion)
    {
        return PhpVersionPattern().IsMatch(phpVersion);
    }

    /// <summary>Whether <paramref name="label"/> is a database label the agent accepts: a letter, then up to fourteen letters or digits.</summary>
    /// <param name="label">The label, already trimmed.</param>
    /// <returns><see langword="true"/> when the shape is valid.</returns>
    public static bool IsDatabaseLabel(string label)
    {
        return LabelPattern().IsMatch(label);
    }

    /// <summary>The absolute path of a website's document root on the server.</summary>
    /// <param name="accountUsername">The hosting account.</param>
    /// <param name="documentRoot">The document root, relative to the account's home.</param>
    /// <returns>The path under <see cref="HomeRoot"/>.</returns>
    public static string DocumentRootPath(string accountUsername, string documentRoot)
    {
        return $"{HomeRoot}/{accountUsername}/{documentRoot}";
    }

    /// <summary>The agent's pattern for a domain name.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostnamePattern();

    /// <summary>The agent's pattern for a document root: one to six segments, none starting with a dot.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}(?:/[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}){0,5}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRootPattern();

    /// <summary>The agent's pattern for a PHP version.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[0-9]\.[0-9]{1,2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PhpVersionPattern();

    /// <summary>The agent's pattern for a database label.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[a-z][a-z0-9]{0,14}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LabelPattern();
}
