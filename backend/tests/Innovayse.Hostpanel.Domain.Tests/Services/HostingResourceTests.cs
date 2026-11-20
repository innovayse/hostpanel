using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>State machine, plugin ownership and server binding shared by <see cref="ServiceWebsite"/> and <see cref="ServiceDatabase"/>.</summary>
public sealed class HostingResourceTests
{
    /// <summary>Creates a website for account <c>web</c> on server 1, issued to plugin <c>acme.sites</c>.</summary>
    /// <returns>A fresh pending website.</returns>
    private static ServiceWebsite NewWebsite()
    {
        return ServiceWebsite.Create(10, 1, "web", "acme.sites", " Example.COM ", " /home/web/example ", "8.3", true);
    }

    /// <summary>Creates a database for account <c>web</c> on server 1, issued to plugin <c>acme.sites</c>.</summary>
    /// <returns>A fresh pending database.</returns>
    private static ServiceDatabase NewDatabase()
    {
        return ServiceDatabase.Create(10, 1, "web", "acme.sites", " shop ");
    }

    /// <summary>A new website is pending, has a stable id and keeps a trimmed, lower-case host name.</summary>
    [Fact]
    public void CreateWebsite_IsPendingWithStableIdAndNormalisedTexts()
    {
        var website = NewWebsite();

        Assert.Equal(HostingResourceState.Pending, website.State);
        Assert.NotEqual(Guid.Empty, website.WebsiteId);
        Assert.Equal("example.com", website.Hostname);
        Assert.Equal("/home/web/example", website.DocumentRoot);
        Assert.Equal("8.3", website.PhpVersion);
    }

    /// <summary>A website may run no PHP at all.</summary>
    [Fact]
    public void CreateWebsite_AllowsNoPhpVersion()
    {
        var website = ServiceWebsite.Create(10, 1, "web", "acme.sites", "example.com", "/home/web", null, false);

        Assert.Null(website.PhpVersion);
    }

    /// <summary>A new database is pending with a stable id, a trimmed label and no server names yet.</summary>
    [Fact]
    public void CreateDatabase_IsPendingWithStableIdAndTrimmedLabel()
    {
        var database = NewDatabase();

        Assert.Equal(HostingResourceState.Pending, database.State);
        Assert.NotEqual(Guid.Empty, database.DatabaseId);
        Assert.Equal("shop", database.Label);
        Assert.Equal(string.Empty, database.Name);
        Assert.Equal(string.Empty, database.Username);
    }

    /// <summary>A website refuses a blank required text.</summary>
    /// <param name="accountUsername">The account.</param>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="hostname">The host name.</param>
    /// <param name="documentRoot">The document root.</param>
    /// <param name="phpVersion">The PHP version.</param>
    [Theory]
    [InlineData(" ", "acme.sites", "example.com", "/home/web", null)]
    [InlineData("web", "", "example.com", "/home/web", null)]
    [InlineData("web", "acme.sites", " ", "/home/web", null)]
    [InlineData("web", "acme.sites", "example.com", "", null)]
    [InlineData("web", "acme.sites", "example.com", "/home/web", " ")]
    public void CreateWebsite_RefusesBlankTexts(
        string accountUsername, string pluginId, string hostname, string documentRoot, string? phpVersion)
    {
        Assert.Throws<ArgumentException>(
            () => ServiceWebsite.Create(10, 1, accountUsername, pluginId, hostname, documentRoot, phpVersion, false));
    }

    /// <summary>A database refuses a blank required text.</summary>
    /// <param name="accountUsername">The account.</param>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="label">The label.</param>
    [Theory]
    [InlineData("", "acme.sites", "shop")]
    [InlineData("web", " ", "shop")]
    [InlineData("web", "acme.sites", " ")]
    public void CreateDatabase_RefusesBlankTexts(string accountUsername, string pluginId, string label)
    {
        Assert.Throws<ArgumentException>(() => ServiceDatabase.Create(10, 1, accountUsername, pluginId, label));
    }

    /// <summary>A resource is issued only to the exact plugin it was created for.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="expected">Whether the resource is issued to it.</param>
    [Theory]
    [InlineData("acme.sites", true)]
    [InlineData("Acme.Sites", false)]
    [InlineData("other.plugin", false)]
    public void IsIssuedTo_MatchesOnlyTheIssuedPlugin(string pluginId, bool expected)
    {
        Assert.Equal(expected, NewWebsite().IsIssuedTo(pluginId));
    }

    /// <summary>A resource is bound only to the exact server and case-sensitive account it was created for.</summary>
    /// <param name="serverId">The server asked about.</param>
    /// <param name="account">The account asked about.</param>
    /// <param name="expected">Whether the resource should be bound.</param>
    [Theory]
    [InlineData(1, "web", true)]
    [InlineData(2, "web", false)]
    [InlineData(1, "Web", false)]
    [InlineData(null, "web", false)]
    [InlineData(1, null, false)]
    public void IsBoundTo_RequiresSameServerAndAccount(int? serverId, string? account, bool expected)
    {
        Assert.Equal(expected, NewDatabase().IsBoundTo(serverId, account));
    }

    /// <summary>The provider's confirmation moves a pending resource to active.</summary>
    [Fact]
    public void MarkActive_MovesPendingToActive()
    {
        var website = NewWebsite();

        website.MarkActive();

        Assert.Equal(HostingResourceState.Active, website.State);
    }

    /// <summary>Only a pending resource can become active: an active, failed or removed one cannot.</summary>
    [Fact]
    public void MarkActive_RefusesAnythingButPending()
    {
        var active = NewWebsite();
        active.MarkActive();
        var failed = NewWebsite();
        failed.MarkRemoveFailed();
        var removed = NewWebsite();
        removed.MarkRemoved();

        Assert.Throws<InvalidOperationException>(active.MarkActive);
        Assert.Throws<InvalidOperationException>(failed.MarkActive);
        Assert.Throws<InvalidOperationException>(removed.MarkActive);
    }

    /// <summary>A failed removal leaves the resource retryable: it may fail again and may then be removed.</summary>
    [Fact]
    public void MarkRemoveFailed_KeepsTheResourceRetryable()
    {
        var website = NewWebsite();
        website.MarkActive();

        website.MarkRemoveFailed();
        website.MarkRemoveFailed();
        Assert.Equal(HostingResourceState.RemoveFailed, website.State);

        website.MarkRemoved();
        Assert.Equal(HostingResourceState.Removed, website.State);
    }

    /// <summary>A removed resource is terminal: no transition leaves it.</summary>
    [Fact]
    public void Removed_IsTerminal()
    {
        var database = NewDatabase();
        database.MarkRemoved();

        Assert.Throws<InvalidOperationException>(database.MarkRemoved);
        Assert.Throws<InvalidOperationException>(database.MarkRemoveFailed);
        Assert.Throws<InvalidOperationException>(database.MarkActive);
        Assert.Equal(HostingResourceState.Removed, database.State);
    }

    /// <summary>The provider's names are recorded together with the confirmation that the database exists.</summary>
    [Fact]
    public void MarkProvisioned_RecordsNamesAndActivates()
    {
        var database = NewDatabase();

        database.MarkProvisioned("web_shop", "web_shop_u");

        Assert.Equal(HostingResourceState.Active, database.State);
        Assert.Equal("web_shop", database.Name);
        Assert.Equal("web_shop_u", database.Username);
    }

    /// <summary>A blank name is refused before the state moves, and a database already active cannot be provisioned again.</summary>
    [Fact]
    public void MarkProvisioned_RefusesBlankNamesAndANonPendingDatabase()
    {
        var database = NewDatabase();

        Assert.Throws<ArgumentException>(() => database.MarkProvisioned(" ", "user"));
        Assert.Throws<ArgumentException>(() => database.MarkProvisioned("name", ""));
        Assert.Equal(HostingResourceState.Pending, database.State);

        database.MarkProvisioned("name", "user");
        Assert.Throws<InvalidOperationException>(() => database.MarkProvisioned("other", "other"));
        Assert.Equal("name", database.Name);
    }
}
