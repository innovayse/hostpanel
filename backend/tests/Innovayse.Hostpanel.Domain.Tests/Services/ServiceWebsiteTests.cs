using Innovayse.Hostpanel.Domain.Services;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>The specification of a <see cref="ServiceWebsite"/>: comparing it with a request and changing it once confirmed.</summary>
public sealed class ServiceWebsiteTests
{
    /// <summary>Creates an active website of plugin <c>acme.sites</c> with a link root and PHP 8.3.</summary>
    /// <returns>The website.</returns>
    private static ServiceWebsite ActiveWebsite()
    {
        var website = ServiceWebsite.Create(10, 1, "web", "acme.sites", "app.example.com", "sites/app", "8.3", true);
        website.MarkActive();
        return website;
    }

    /// <summary>A request with the same root, PHP version and link flag matches, ignoring surrounding blanks.</summary>
    [Fact]
    public void Matches_IsTrueForTheSameSpecification()
    {
        Assert.True(ActiveWebsite().Matches(" sites/app ", "8.3", true));
    }

    /// <summary>Any one differing part makes the request a change.</summary>
    /// <param name="documentRoot">The root asked for.</param>
    /// <param name="phpVersion">The PHP version asked for.</param>
    /// <param name="mayBeLink">The link flag asked for.</param>
    [Theory]
    [InlineData("sites/other", "8.3", true)]
    [InlineData("sites/app", "8.2", true)]
    [InlineData("sites/app", null, true)]
    [InlineData("sites/app", "8.3", false)]
    public void Matches_IsFalseWhenAnyPartDiffers(string documentRoot, string? phpVersion, bool mayBeLink)
    {
        Assert.False(ActiveWebsite().Matches(documentRoot, phpVersion, mayBeLink));
    }

    /// <summary>A confirmed change replaces all three parts and keeps the host name and the id.</summary>
    [Fact]
    public void Respecify_ReplacesTheSpecificationOnly()
    {
        var website = ActiveWebsite();
        var id = website.WebsiteId;

        website.Respecify(" public_html ", null, false);

        Assert.Equal("public_html", website.DocumentRoot);
        Assert.Null(website.PhpVersion);
        Assert.False(website.DocumentRootMayBeLink);
        Assert.Equal("app.example.com", website.Hostname);
        Assert.Equal(id, website.WebsiteId);
    }

    /// <summary>Only an active website changes: a pending or removed one does not, and a blank root or PHP version is refused.</summary>
    [Fact]
    public void Respecify_RefusesANonActiveWebsiteAndBlankTexts()
    {
        var pending = ServiceWebsite.Create(10, 1, "web", "acme.sites", "app.example.com", "sites/app", null, false);
        var removed = ActiveWebsite();
        removed.MarkRemoved();

        Assert.Throws<InvalidOperationException>(() => pending.Respecify("sites/app", null, false));
        Assert.Throws<InvalidOperationException>(() => removed.Respecify("sites/app", null, false));
        Assert.Throws<ArgumentException>(() => ActiveWebsite().Respecify(" ", null, false));
        Assert.Throws<ArgumentException>(() => ActiveWebsite().Respecify("sites/app", " ", false));
    }
}
