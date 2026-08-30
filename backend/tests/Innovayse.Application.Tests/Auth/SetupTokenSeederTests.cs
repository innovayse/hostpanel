namespace Innovayse.Application.Tests.Auth;

using Innovayse.Application.Auth.Services;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Unit tests for <see cref="SetupTokenSeeder"/>.
/// <para>
/// The property that matters most here is the one that is easiest to get wrong: the token must
/// <b>not</b> rotate on every boot. Rotating it would invalidate what an operator had already
/// copied, so restarting the container halfway through setup would lock them out of the step
/// they were in the middle of — which is exactly the failure mode this gate must not introduce.
/// </para>
/// </summary>
public sealed class SetupTokenSeederTests
{
    /// <summary>
    /// Builds the seeder's dependencies over mocks.
    /// </summary>
    /// <param name="existing">The token row already in the settings table, if any.</param>
    /// <param name="adminHeld">Whether somebody already holds the Admin role.</param>
    /// <returns>The repository mock, the role store mock and the unit of work mock.</returns>
    private static (Mock<ISettingRepository> Settings, Mock<ISubjectRoleStore> Roles, Mock<IUnitOfWork> Uow)
        Build(Setting? existing, bool adminHeld)
    {
        var settings = new Mock<ISettingRepository>();
        settings.Setup(s => s.FindByKeyAsync(SetupTokenSeeder.SettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var roles = new Mock<ISubjectRoleStore>();
        roles.Setup(r => r.AnyHasRoleAsync(Roles.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminHeld);

        return (settings, roles, new Mock<IUnitOfWork>());
    }

    /// <summary>On a fresh install a token is issued, persisted and returned.</summary>
    [Fact]
    public async Task EnsureIssued_OnFreshInstall_IssuesAndPersistsAsync()
    {
        var (settings, roles, uow) = Build(existing: null, adminHeld: false);
        Setting? added = null;
        settings.Setup(s => s.Add(It.IsAny<Setting>())).Callback<Setting>(s => added = s);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(settings.Object, roles.Object, uow.Object);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotNull(added);
        Assert.Equal(SetupTokenSeeder.SettingKey, added!.Key);
        Assert.Equal(token, added.Value);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A restart while setup is still outstanding re-announces the same token rather than
    /// issuing a new one. This is what makes a container restart mid-setup recoverable.
    /// </summary>
    [Fact]
    public async Task EnsureIssued_WhenTokenOutstanding_ReturnsTheSameOneAsync()
    {
        var outstanding = Setting.Create(SetupTokenSeeder.SettingKey, "already-issued", null);
        var (settings, roles, uow) = Build(existing: outstanding, adminHeld: false);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(settings.Object, roles.Object, uow.Object);

        Assert.Equal("already-issued", token);
        settings.Verify(s => s.Add(It.IsAny<Setting>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Once somebody holds Admin nothing is issued and nothing is announced — a completed
    /// installation must not keep printing a claimable secret to its log.
    /// </summary>
    [Fact]
    public async Task EnsureIssued_WhenAdminHeld_IssuesNothingAsync()
    {
        var (settings, roles, uow) = Build(existing: null, adminHeld: true);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(settings.Object, roles.Object, uow.Object);

        Assert.Null(token);
        settings.Verify(s => s.Add(It.IsAny<Setting>()), Times.Never);
        settings.Verify(
            s => s.FindByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A spent row on an installation that is unclaimed again — setup completed, then every
    /// Admin grant revoked — is re-armed rather than left as a permanent lockout.
    /// </summary>
    [Fact]
    public async Task EnsureIssued_WhenRowSpentAndAdminUnheld_ReArmsAsync()
    {
        var spent = Setting.Create(SetupTokenSeeder.SettingKey, string.Empty, null);
        var (settings, roles, uow) = Build(existing: spent, adminHeld: false);

        var token = await SetupTokenSeeder.EnsureIssuedAsync(settings.Object, roles.Object, uow.Object);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(token, spent.Value);
        settings.Verify(s => s.Add(It.IsAny<Setting>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Two issues never collide. The value is 256 bits of cryptographic randomness; this pins
    /// that it is generated rather than derived from anything stable.
    /// </summary>
    [Fact]
    public async Task EnsureIssued_IssuesADistinctTokenPerInstallationAsync()
    {
        var (settingsA, rolesA, uowA) = Build(existing: null, adminHeld: false);
        var (settingsB, rolesB, uowB) = Build(existing: null, adminHeld: false);

        var first = await SetupTokenSeeder.EnsureIssuedAsync(settingsA.Object, rolesA.Object, uowA.Object);
        var second = await SetupTokenSeeder.EnsureIssuedAsync(settingsB.Object, rolesB.Object, uowB.Object);

        Assert.NotEqual(first, second);
    }
}
