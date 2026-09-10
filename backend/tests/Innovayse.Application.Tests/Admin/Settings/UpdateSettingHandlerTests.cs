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
}
