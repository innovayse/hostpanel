namespace Innovayse.Application.Tests.Auth;

using Innovayse.Application.Auth.Commands.CompleteSetup;
using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Auth.Services;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CompleteSetupHandler"/> — the first-run bootstrap.
/// <para>
/// <b>Both modes are covered by construction.</b> The gate this handler applies exists for
/// standalone (<c>local</c>) installs, where registration is public and whoever claimed the
/// Admin role first would own a box that was reachable before its owner had finished
/// configuring it. Under <c>sso</c> it must be inert: accounts live in the sign-on service,
/// no token is ever issued, and that path is in production use. Every test below fixes
/// <see cref="IAuthModeProvider.IsLocalMode"/> explicitly for that reason.
/// </para>
/// </summary>
public sealed class CompleteSetupHandlerTests
{
    /// <summary>Subject of the caller in these tests.</summary>
    private const string Subject = "user-1";

    /// <summary>The token a test installation has outstanding.</summary>
    private const string IssuedToken = "the-issued-setup-token";

    /// <summary>
    /// Builds the handler over mocks, with the pieces a test wants to inspect handed back.
    /// </summary>
    /// <param name="isLocalMode">Which deployment shape to stand up.</param>
    /// <param name="storedToken">
    /// The outstanding token row, or <see langword="null"/> for an installation with none.
    /// </param>
    /// <param name="adminHeld">Whether somebody already holds the Admin role.</param>
    /// <param name="subject">The caller's subject, or <see langword="null"/> for none.</param>
    /// <returns>The handler and the role store and setting it was built over.</returns>
    private static (CompleteSetupHandler Handler, Mock<ISubjectRoleStore> Roles, Setting? Token)
        Build(bool isLocalMode, string? storedToken, bool adminHeld = false, string? subject = Subject)
    {
        var roles = new Mock<ISubjectRoleStore>();
        roles.Setup(r => r.AnyHasRoleAsync(Roles.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminHeld);

        var tokenSetting = storedToken is null
            ? null
            : Setting.Create(SetupTokenSeeder.SettingKey, storedToken, null);

        var settings = new Mock<ISettingRepository>();
        settings.Setup(s => s.FindByKeyAsync(SetupTokenSeeder.SettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenSetting);

        var mode = new Mock<IAuthModeProvider>();
        mode.SetupGet(m => m.IsLocalMode).Returns(isLocalMode);

        var caller = new Mock<ICurrentRequestContext>();
        caller.SetupGet(c => c.UserId).Returns(subject);

        var handler = new CompleteSetupHandler(
            roles.Object, settings.Object, mode.Object, new Mock<IUnitOfWork>().Object, caller.Object);

        return (handler, roles, tokenSetting);
    }

    /// <summary>
    /// Local mode, correct token: the role is granted and the token is retired so it cannot be
    /// replayed.
    /// </summary>
    [Fact]
    public async Task Handle_LocalModeWithCorrectToken_GrantsAdminAndRetiresTokenAsync()
    {
        var (handler, roles, token) = Build(isLocalMode: true, storedToken: IssuedToken);

        await handler.HandleAsync(new CompleteSetupCommand(IssuedToken), CancellationToken.None);

        roles.Verify(r => r.AddAsync(Subject, Roles.Admin, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(string.Empty, token!.Value);
    }

    /// <summary>
    /// Local mode, wrong token: refused, and — the point of the whole gate — no role granted.
    /// </summary>
    [Fact]
    public async Task Handle_LocalModeWithWrongToken_RefusesAndGrantsNothingAsync()
    {
        var (handler, roles, _) = Build(isLocalMode: true, storedToken: IssuedToken);

        await Assert.ThrowsAsync<SetupTokenInvalidException>(() =>
            handler.HandleAsync(new CompleteSetupCommand("not-the-token"), CancellationToken.None));

        roles.Verify(
            r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Local mode, no token presented: this is the exact request that used to succeed and hand
    /// the installation to whoever sent it first.
    /// </summary>
    [Fact]
    public async Task Handle_LocalModeWithNoToken_RefusesAndGrantsNothingAsync()
    {
        var (handler, roles, _) = Build(isLocalMode: true, storedToken: IssuedToken);

        await Assert.ThrowsAsync<SetupTokenInvalidException>(() =>
            handler.HandleAsync(new CompleteSetupCommand(null), CancellationToken.None));

        roles.Verify(
            r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Local mode with a token row that was already spent: fails closed rather than treating an
    /// empty stored value as "no gate configured, let them through".
    /// </summary>
    [Fact]
    public async Task Handle_LocalModeWithSpentToken_RefusesAsync()
    {
        var (handler, roles, _) = Build(isLocalMode: true, storedToken: string.Empty);

        await Assert.ThrowsAsync<SetupTokenInvalidException>(() =>
            handler.HandleAsync(new CompleteSetupCommand(string.Empty), CancellationToken.None));

        roles.Verify(
            r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// <b>The SSO half of the contract.</b> No token exists there and none is asked for, so a
    /// claim with no token still grants — which is the behaviour that shipped, unchanged.
    /// </summary>
    [Fact]
    public async Task Handle_SsoModeWithNoToken_GrantsAdminAsync()
    {
        var (handler, roles, _) = Build(isLocalMode: false, storedToken: null);

        await handler.HandleAsync(new CompleteSetupCommand(null), CancellationToken.None);

        roles.Verify(r => r.AddAsync(Subject, Roles.Admin, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Somebody already holds Admin: refused in either mode, and refused before the token is
    /// looked at so the two refusals cannot be used to probe whether the box is still claimable.
    /// </summary>
    /// <param name="isLocalMode">The deployment shape under test.</param>
    /// <returns>A task representing the assertion.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WhenAdminAlreadyHeld_RefusesAsync(bool isLocalMode)
    {
        var (handler, roles, _) = Build(isLocalMode, storedToken: IssuedToken, adminHeld: true);

        await Assert.ThrowsAsync<SetupAlreadyCompletedException>(() =>
            handler.HandleAsync(new CompleteSetupCommand(IssuedToken), CancellationToken.None));

        roles.Verify(
            r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A request with a valid token but no subject grants nothing. The endpoint is
    /// <c>[Authorize]</c>, so this should be unreachable; it is asserted because "holding the
    /// token" must never be the whole of what makes somebody Admin.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutSubject_RefusesAsync()
    {
        var (handler, roles, _) = Build(isLocalMode: true, storedToken: IssuedToken, subject: null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.HandleAsync(new CompleteSetupCommand(IssuedToken), CancellationToken.None));

        roles.Verify(
            r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
