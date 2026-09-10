namespace Innovayse.Application.Tests.Admin.Settings;

using Innovayse.Application.Admin.Queries.GetPublicSettings;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Tests for <see cref="GetPublicSettingsHandler"/>: the anonymous read must return the
/// allow-listed keys with a value and nothing else — the same table holds credentials.
/// </summary>
public class GetPublicSettingsHandlerTests
{
    private readonly Mock<ISettingRepository> repo = new();

    private GetPublicSettingsHandler CreateHandler() => new(repo.Object);

    /// <summary>Makes <c>ListAsync</c> return the given rows.</summary>
    /// <param name="rows">Key/value pairs to store.</param>
    private void SeedRows(params (string Key, string Value)[] rows)
    {
        var stored = rows.Select(r => Setting.Create(r.Key, r.Value, null)).ToList();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
    }

    [Fact]
    public async Task HandleAsync_NeverReturnsAnIntegrationRow()
    {
        SeedRows(
            ("integration:innovayse-inecobank:password", "secret"),
            (PortalSettingKeys.Template, "nova"));

        var result = await CreateHandler().HandleAsync(new GetPublicSettingsQuery(), CancellationToken.None);

        var single = Assert.Single(result);
        Assert.Equal(PortalSettingKeys.Template, single.Key);
        Assert.Equal("nova", single.Value);
    }

    [Fact]
    public async Task HandleAsync_DropsAllowListedRowsWithBlankValue()
    {
        SeedRows(
            (PortalSettingKeys.LogoDark, "   "),
            (PortalSettingKeys.Logo, ""),
            (PortalSettingKeys.SiteName, "Acme Hosting"));

        var result = await CreateHandler().HandleAsync(new GetPublicSettingsQuery(), CancellationToken.None);

        var single = Assert.Single(result);
        Assert.Equal(PortalSettingKeys.SiteName, single.Key);
    }

    [Fact]
    public async Task HandleAsync_UnknownPortalPrefixedKey_IsNotExposed()
    {
        // The reason the list is a set and not a prefix match.
        SeedRows(("portal.internal.api_secret", "do-not-leak"));

        var result = await CreateHandler().HandleAsync(new GetPublicSettingsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEveryNewPhaseOneKeyWhenSet()
    {
        SeedRows(
            (PortalSettingKeys.LogoDark, "https://cdn/dark.png"),
            (PortalSettingKeys.LogoMark, "https://cdn/mark.png"),
            (PortalSettingKeys.ThemeDefault, "system"),
            (PortalSettingKeys.ThemeUserToggle, "false"),
            (PortalSettingKeys.SiteName, "Acme"),
            (PortalSettingKeys.SiteTagline, "Hosting"));

        var result = await CreateHandler().HandleAsync(new GetPublicSettingsQuery(), CancellationToken.None);

        Assert.Equal(6, result.Count);
    }
}
