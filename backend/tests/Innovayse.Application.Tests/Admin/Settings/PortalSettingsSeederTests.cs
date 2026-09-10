namespace Innovayse.Application.Tests.Admin.Settings;

using Innovayse.Application.Admin.Services;
using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Tests for <see cref="PortalSettingsSeeder"/>: it fills in what is missing and never
/// touches a row an operator already has.
/// </summary>
public class PortalSettingsSeederTests
{
    private readonly Mock<ISettingRepository> repo = new();
    private readonly Mock<IUnitOfWork> uow = new();
    private readonly List<Setting> added = [];

    /// <summary>Wires the repository so existing rows answer <c>FindByKeyAsync</c> and additions are captured.</summary>
    /// <param name="existing">Rows already in the table.</param>
    private void SeedExisting(params Setting[] existing)
    {
        repo.Setup(r => r.FindByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => existing.FirstOrDefault(s => s.Key == key));
        repo.Setup(r => r.Add(It.IsAny<Setting>())).Callback<Setting>(added.Add);
    }

    [Fact]
    public async Task EnsureSeededAsync_EmptyTable_AddsEveryDefaultOnce()
    {
        SeedExisting();

        var created = await PortalSettingsSeeder.EnsureSeededAsync(repo.Object, uow.Object);

        Assert.Equal(PortalSettingKeys.Defaults.Count, created);
        Assert.Equal(
            PortalSettingKeys.Defaults.Select(d => d.Key).OrderBy(k => k),
            added.Select(s => s.Key).OrderBy(k => k));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureSeededAsync_ExistingChoice_IsLeftAloneAndOnlyMissingKeysAdded()
    {
        var chosen = Setting.Create(PortalSettingKeys.Template, "classic", null);
        SeedExisting(chosen);

        var created = await PortalSettingsSeeder.EnsureSeededAsync(repo.Object, uow.Object);

        Assert.Equal(PortalSettingKeys.Defaults.Count - 1, created);
        Assert.Equal("classic", chosen.Value);
        Assert.DoesNotContain(added, s => s.Key == PortalSettingKeys.Template);
    }

    [Fact]
    public async Task EnsureSeededAsync_NothingMissing_DoesNotSave()
    {
        SeedExisting(PortalSettingKeys.Defaults.Select(d => Setting.Create(d.Key, d.Value, d.Description)).ToArray());

        var created = await PortalSettingsSeeder.EnsureSeededAsync(repo.Object, uow.Object);

        Assert.Equal(0, created);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSeededAsync_NewInstall_StartsDarkWithToggleOn()
    {
        // The pre-existing behaviour of the storefront, so an upgrade changes nothing visible.
        SeedExisting();

        await PortalSettingsSeeder.EnsureSeededAsync(repo.Object, uow.Object);

        Assert.Equal("dark", added.Single(s => s.Key == PortalSettingKeys.ThemeDefault).Value);
        Assert.Equal("true", added.Single(s => s.Key == PortalSettingKeys.ThemeUserToggle).Value);
    }
}
