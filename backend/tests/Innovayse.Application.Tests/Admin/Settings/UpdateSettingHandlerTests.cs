namespace Innovayse.Application.Tests.Admin.Settings;

using Innovayse.Application.Admin.Commands.UpdateSetting;
using Innovayse.Application.Admin.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Tests for <see cref="UpdateSettingHandler"/>: the vocabulary check on fixed-value keys, the
/// canonical spelling it stores, and the pass-through for every other key.
/// </summary>
public class UpdateSettingHandlerTests
{
    private readonly Mock<ISettingRepository> repo = new();
    private readonly Mock<IUnitOfWork> uow = new();

    private UpdateSettingHandler CreateHandler() => new(repo.Object, uow.Object);

    /// <summary>Makes <c>FindByIdAsync(1)</c> return a row with the given key and value.</summary>
    /// <param name="key">The row's key.</param>
    /// <param name="value">The row's current value.</param>
    /// <returns>The row, so a test can read what was stored.</returns>
    private Setting SeedRow(string key, string value = "")
    {
        var setting = Setting.Create(key, value, null);
        repo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(setting);
        return setting;
    }

    [Fact]
    public async Task HandleAsync_UnknownId_ThrowsInvalidOperation()
    {
        repo.Setup(r => r.FindByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Setting?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(99, "x"), CancellationToken.None));

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Template_StoresCanonicalSpelling()
    {
        var row = SeedRow(PortalSettingKeys.Template, "aurora");

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, " Nova "), CancellationToken.None);

        Assert.Equal("nova", row.Value);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Template_UnknownName_IsRefusedAndNothingSaved()
    {
        var row = SeedRow(PortalSettingKeys.Template, "aurora");

        var ex = await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "bootstrap"), CancellationToken.None));

        Assert.Equal(PortalSettingKeys.Template, ex.Key);
        Assert.Contains("nova", ex.Message);
        Assert.Equal("aurora", row.Value);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ThemeDefault_AcceptsSystem()
    {
        var row = SeedRow(PortalSettingKeys.ThemeDefault, "dark");

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "SYSTEM"), CancellationToken.None);

        Assert.Equal("system", row.Value);
    }

    [Fact]
    public async Task HandleAsync_BooleanKey_RefusesThirdSpelling()
    {
        SeedRow(PortalSettingKeys.ThemeUserToggle, "true");

        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "yes"), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_FreeTextKey_StoresTrimmedValueUnchanged()
    {
        var row = SeedRow(PortalSettingKeys.SiteName);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "  Acme Hosting  "), CancellationToken.None);

        Assert.Equal("Acme Hosting", row.Value);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_IntegrationKey_HasNoVocabulary()
    {
        var row = SeedRow("integration:cwp7:hostname");

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "cwp.example.com"), CancellationToken.None);

        Assert.Equal("cwp.example.com", row.Value);
    }

    [Fact]
    public async Task HandleAsync_HexColour_StoredLowerCase()
    {
        var row = SeedRow(PortalSettingKeys.BrandPrimary);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, " #1A73E8 "), CancellationToken.None);

        Assert.Equal("#1a73e8", row.Value);
    }

    [Fact]
    public async Task HandleAsync_HexColour_WithoutHash_IsRefusedWithExpectedShape()
    {
        SeedRow(PortalSettingKeys.BrandPrimary);

        var ex = await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "1a73e8"), CancellationToken.None));

        Assert.Contains("Expected: a colour like #1a73e8", ex.Message);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShapedKey_AcceptsEmptyAsUnset()
    {
        var row = SeedRow(PortalSettingKeys.BrandAccent, "#a855f7");

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, ""), CancellationToken.None);

        Assert.Equal("", row.Value);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Template_RefusesEmpty()
    {
        SeedRow(PortalSettingKeys.Template, "aurora");

        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, ""), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Font_AcceptsEmptyAndVocabulary()
    {
        var row = SeedRow(PortalSettingKeys.BrandFontHeading);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "Noto-Serif-Armenian"), CancellationToken.None);
        Assert.Equal("noto-serif-armenian", row.Value);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, ""), CancellationToken.None);
        Assert.Equal("", row.Value);

        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "comic-sans"), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_GtmId_StoredUpperCase_AndShapeChecked()
    {
        var row = SeedRow(PortalSettingKeys.AnalyticsGtmId);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "gtm-abc123"), CancellationToken.None);
        Assert.Equal("GTM-ABC123", row.Value);

        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "UA-12345-1"), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ChatBaseUrl_RequiresHttps_AndDropsTrailingSlash()
    {
        var row = SeedRow(PortalSettingKeys.ChatBaseUrl);

        await CreateHandler().HandleAsync(new UpdateSettingCommand(1, "https://chat.example.com/"), CancellationToken.None);
        Assert.Equal("https://chat.example.com", row.Value);

        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "http://chat.example.com"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidSettingValueException>(() =>
            CreateHandler().HandleAsync(new UpdateSettingCommand(1, "chat.example.com"), CancellationToken.None));
    }
}
