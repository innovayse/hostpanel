namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// The one rule a panel sign in link must meet before it is handed to a browser, whichever plugin issued it: a secure address with
/// no user info, on the server's own host. A plugin may check its link too, but the host never relies on it, so a link that points
/// elsewhere (a phishing page, an internal service) is refused here for every panel.
/// </summary>
public static class PanelSsoLinkRules
{
    /// <summary>Accepts a link only when it is an https address with no user info on one of the server's own hosts.</summary>
    /// <param name="link">The address the plugin answered.</param>
    /// <param name="ownHosts">The host names that belong to the server: the host the panel answers on and the host its API is dialled at.</param>
    /// <returns>The parsed address, or <see langword="null"/> when it breaks the rule.</returns>
    public static Uri? Accept(string? link, params string?[] ownHosts)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0)
        {
            return null;
        }

        return ownHosts.Any(host => !string.IsNullOrEmpty(host) && string.Equals(host, uri.IdnHost, StringComparison.OrdinalIgnoreCase))
            ? uri
            : null;
    }
}
