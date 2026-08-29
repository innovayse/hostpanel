namespace Innovayse.Application.Tests.Support;

using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.Queries.GetKbArticle;
using Innovayse.Application.Support.Queries.GetPublishedKbArticle;
using Innovayse.Application.Support.Queries.ListPublishedAnnouncements;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Proves the client-facing announcement and knowledge base reads show clients what they may see
/// and nothing else.
/// <para>
/// Before this, the portal's "Recent News" card called an endpoint that was
/// <c>[Authorize(Roles = Roles.Admin)]</c>, so every real customer got 403 and an empty card. The
/// obvious repair -- pointing the portal at the admin read -- would have traded a 403 for a leak:
/// that read returns unpublished rows and the editorial <c>IsPublished</c> flag. These tests
/// assert both halves: a client can read published announcements, and the projection they receive
/// has no way to carry a draft or say that drafts exist. The knowledge base's public by-id route
/// had the mirror-image fault and is covered here too.
/// </para>
/// </summary>
public sealed class PublishedContentVisibilityTests
{
    /// <summary>The article id every knowledge base probe asks for.</summary>
    private const int ArticleId = 12;

    /// <summary>
    /// A client asking for announcements gets the published ones, through a repository call that
    /// cannot return a draft.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ListPublishedAnnouncements_ReturnsPublishedRows()
    {
        var repo = new Mock<IAnnouncementRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ListPublishedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Announcement> { Announcement.Create("Maintenance", "Sunday 02:00 UTC", true) }, 1));

        var result = await new ListPublishedAnnouncementsHandler(repo.Object)
            .HandleAsync(new ListPublishedAnnouncementsQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Maintenance", Assert.Single(result.Items).Title);

        // MockBehavior.Strict already fails on any unconfigured call; naming ListAsync explicitly
        // states the point: the client read never reaches the admin, unfiltered listing.
        repo.Verify(r => r.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.VerifyAll();
    }

    /// <summary>
    /// The client read asks the repository for published rows only, so an unpublished
    /// announcement has no path to the portal even if one exists alongside it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task ListPublishedAnnouncements_NeverSeesDrafts()
    {
        var published = Announcement.Create("Released", "Visible", true);
        var draft = Announcement.Create("Embargoed price rise", "Do not tell them yet", false);

        var repo = new Mock<IAnnouncementRepository>();

        // The repository is the boundary the filter lives behind: ListPublishedAsync answers with
        // the published row alone, while the admin ListAsync would have handed over both.
        repo.Setup(r => r.ListPublishedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Announcement> { published }, 1));
        repo.Setup(r => r.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Announcement> { published, draft }, 2));

        var result = await new ListPublishedAnnouncementsHandler(repo.Object)
            .HandleAsync(new ListPublishedAnnouncementsQuery(1, 20), CancellationToken.None);

        Assert.DoesNotContain(result.Items, a => a.Title == draft.Title);
        Assert.Equal(1, result.TotalCount);
    }

    /// <summary>
    /// The projection handed to a client carries no published/draft flag, while the admin one
    /// still does. Asserted over the type rather than an instance so re-adding the field to the
    /// client DTO breaks here rather than in production.
    /// </summary>
    [Fact]
    public void PublishedAnnouncementDto_CarriesNoEditorialFlag()
    {
        var clientFields = typeof(PublishedAnnouncementDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("IsPublished", clientFields);
        Assert.DoesNotContain(typeof(PublishedAnnouncementDto).GetProperties(), p => p.PropertyType == typeof(bool));

        // The split is only meaningful if the admin projection really does expose more.
        Assert.Contains("IsPublished", typeof(AnnouncementDto).GetProperties().Select(p => p.Name));
    }

    /// <summary>
    /// The public knowledge base route refuses an unpublished article, and refuses it without
    /// performing the read that would have projected it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GetPublishedKbArticle_RefusesDraft()
    {
        var draft = KbArticle.Create("Internal runbook", "root password rotation", "Ops");
        Assert.False(draft.IsPublished);

        var repo = new Mock<IKbArticleRepository>();
        repo.Setup(r => r.FindByIdAsync(ArticleId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetPublishedKbArticleHandler(repo.Object, bus.Object)
                .HandleAsync(new GetPublishedKbArticleQuery(ArticleId), CancellationToken.None));

        // Strict and unconfigured: the shared projection is never dispatched for a draft.
        bus.Verify(b => b.InvokeAsync<KbArticleDto>(It.Is<object>(m => m is GetKbArticleQuery), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Never);

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetPublishedKbArticleHandler(MissingArticleRepo(), bus.Object)
                .HandleAsync(new GetPublishedKbArticleQuery(ArticleId), CancellationToken.None));

        // Same wording for "draft" and "no such row": ids are sequential and this route is
        // anonymous, so a distinguishable refusal is an enumeration oracle.
        Assert.Equal(missing.Message, refusal.Message);
    }

    /// <summary>A published article still reaches the public route, through the shared projection.</summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GetPublishedKbArticle_ReturnsPublishedArticle()
    {
        var article = KbArticle.Create("How to reset your password", "Click reset.", "Accounts");
        article.Publish();

        var repo = new Mock<IKbArticleRepository>();
        repo.Setup(r => r.FindByIdAsync(ArticleId, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var expected = new KbArticleDto(ArticleId, article.Title, article.Content, article.Category, true);
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<KbArticleDto>(It.Is<object>(m => m is GetKbArticleQuery), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(expected);

        var dto = await new GetPublishedKbArticleHandler(repo.Object, bus.Object)
            .HandleAsync(new GetPublishedKbArticleQuery(ArticleId), CancellationToken.None);

        Assert.Equal(expected, dto);
    }

    /// <summary>Builds a repository that knows of no article at <see cref="ArticleId"/>.</summary>
    /// <returns>A repository answering <see langword="null"/>.</returns>
    private static IKbArticleRepository MissingArticleRepo()
    {
        var repo = new Mock<IKbArticleRepository>();
        repo.Setup(r => r.FindByIdAsync(ArticleId, It.IsAny<CancellationToken>())).ReturnsAsync((KbArticle?)null);
        return repo.Object;
    }
}
