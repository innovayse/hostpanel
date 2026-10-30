using Innovayse.Hostpanel.Domain.Admin;

namespace Innovayse.Hostpanel.Domain.Tests.Admin;

/// <summary>Unit tests for the <see cref="LatestRelease"/> entity.</summary>
public sealed class LatestReleaseTests
{
    private static readonly DateTimeOffset Published = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Checked = new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// <see cref="LatestRelease.Create"/> returns a row with the singleton id and the supplied fields.
    /// </summary>
    [Fact]
    public void Create_SetsAllPropertiesAndSingletonId()
    {
        var release = LatestRelease.Create("1.5.0", Published, "https://example.com/releases/v1.5.0", Checked);

        release.Id.Should().Be(LatestRelease.SingletonId);
        release.Version.Should().Be("1.5.0");
        release.PublishedAt.Should().Be(Published);
        release.ReleaseNotesUrl.Should().Be("https://example.com/releases/v1.5.0");
        release.CheckedAt.Should().Be(Checked);
    }

    /// <summary>
    /// <see cref="LatestRelease.Update"/> overwrites every data field without changing the id.
    /// </summary>
    [Fact]
    public void Update_OverwritesDataFields()
    {
        var release = LatestRelease.Create("1.0.0", Published, "https://example.com/releases/v1.0.0", Checked);
        var newPublished = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var newChecked = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

        release.Update("2.0.0", newPublished, "https://example.com/releases/v2.0.0", newChecked);

        release.Id.Should().Be(LatestRelease.SingletonId);
        release.Version.Should().Be("2.0.0");
        release.PublishedAt.Should().Be(newPublished);
        release.ReleaseNotesUrl.Should().Be("https://example.com/releases/v2.0.0");
        release.CheckedAt.Should().Be(newChecked);
    }

    /// <summary>The singleton id is always 1.</summary>
    [Fact]
    public void SingletonId_IsOne()
    {
        LatestRelease.SingletonId.Should().Be(1);
    }
}
