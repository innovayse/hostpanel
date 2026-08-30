namespace Innovayse.Application.Tests.Clients;

using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Tests for <see cref="UpdateClientHandler"/>, covering how it decides whether a save
/// is trying to move the account to a different sign-in address.
/// </summary>
public class UpdateClientHandlerTests
{
    /// <summary>The subject the fixture client is linked to.</summary>
    private const string Subject = "user-16";

    /// <summary>The address that account currently signs in with.</summary>
    private const string CurrentEmail = "anahitakv@example.com";

    /// <summary>
    /// Builds the command the account form posts: every field populated, including the
    /// address, because the form reads it back and sends it whether or not it was edited.
    /// </summary>
    /// <param name="email">The address the form posts.</param>
    /// <returns>A command ready to hand to the handler.</returns>
    /// <param name="language">The language code the form posts, or null when it posts none.</param>
    private static UpdateClientCommand Command(string? email, string? language = null) => new(
        ClientId: 16,
        Email: email,
        FirstName: "Roots",
        LastName: "Agency",
        CompanyName: null,
        Phone: null,
        Street: null,
        Address2: null,
        City: null,
        State: null,
        PostCode: null,
        Country: null,
        Language: language,
        Currency: null,
        PaymentMethod: null,
        BillingContact: null,
        AdminNotes: null,
        NotifyGeneral: true,
        NotifyInvoice: true,
        NotifySupport: true,
        NotifyProduct: true,
        NotifyDomain: true,
        NotifyAffiliate: true,
        LateFees: true,
        OverdueNotices: true,
        TaxExempt: false,
        SeparateInvoices: false,
        DisableCcProcessing: false,
        MarketingOptIn: false,
        StatusUpdate: true,
        AllowSso: true,
        Status: null);

    /// <summary>
    /// Assembles the handler over a client linked to <see cref="Subject"/>, an identity
    /// provider that answers with <see cref="CurrentEmail"/>, and a provisioner that
    /// refuses every write the way an SSO-mode deployment's does.
    /// </summary>
    /// <param name="provisioning">Receives the mocked provisioner, for verification.</param>
    /// <returns>The handler under test.</returns>
    private static UpdateClientHandler SsoModeHandler(out Mock<IUserProvisioning> provisioning)
    {
        var client = Client.Create(Subject, "Roots", "Agency", CurrentEmail);

        var repo = new Mock<IClientRepository>();
        repo.Setup(r => r.FindByIdAsync(16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.FindBySubjectAsync(Subject, It.IsAny<CancellationToken>()))
            // Language null, which is what an SSO-backed provider answers: it does not hand
            // one out, so hostpanel holds none for this person.
            .ReturnsAsync(new IdentityAccount(Subject, CurrentEmail, "Roots", "Agency"));

        provisioning = new Mock<IUserProvisioning>();
        provisioning
            .Setup(p => p.ChangeEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserProvisioningNotAllowedException(UserProvisioningOperation.ChangeEmail));
        provisioning
            .Setup(p => p.UpdateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserProvisioningNotAllowedException(UserProvisioningOperation.ChangeName));

        return new UpdateClientHandler(
            repo.Object, Mock.Of<IUnitOfWork>(), provisioning.Object, identity.Object);
    }

    /// <summary>
    /// Assembles the handler over a deployment that owns its own accounts: the provisioner
    /// writes rather than refuses, and the identity provider answers with the language the
    /// account row currently holds.
    /// </summary>
    /// <param name="currentLanguage">What the account row holds before the save.</param>
    /// <param name="provisioning">Receives the mocked provisioner, for verification.</param>
    /// <returns>The handler under test.</returns>
    private static UpdateClientHandler LocalModeHandler(
        string? currentLanguage, out Mock<IUserProvisioning> provisioning)
    {
        var client = Client.Create(Subject, "Roots", "Agency", CurrentEmail);

        var repo = new Mock<IClientRepository>();
        repo.Setup(r => r.FindByIdAsync(16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.FindBySubjectAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccount(
                Subject, CurrentEmail, "Roots", "Agency", Language: currentLanguage));

        // Not configured to throw: a deployment that owns its accounts writes them.
        provisioning = new Mock<IUserProvisioning>();

        return new UpdateClientHandler(
            repo.Object, Mock.Of<IUnitOfWork>(), provisioning.Object, identity.Object);
    }

    /// <summary>
    /// A changed language reaches the provisioner under local mode, which is the only mode
    /// where this product owns the account row the language is stored on. The names it passes
    /// are the account's existing ones: setting a language must not double as a rename.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_LanguageChangedUnderLocalMode_IsWrittenWithoutRenaming()
    {
        var handler = LocalModeHandler(currentLanguage: "en", out var provisioning);

        await handler.HandleAsync(Command(CurrentEmail, language: "hy"), CancellationToken.None);

        provisioning.Verify(
            p => p.UpdateProfileAsync(Subject, "Roots", "Agency", "hy", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The same guard the address has: the account form posts the language on every save, so
    /// a save that did not touch it must not write anything.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_LanguageUnchangedUnderLocalMode_DoesNotWrite()
    {
        var handler = LocalModeHandler(currentLanguage: "ru", out var provisioning);

        await handler.HandleAsync(Command(CurrentEmail, language: "ru"), CancellationToken.None);

        provisioning.Verify(
            p => p.UpdateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Under SSO mode hostpanel holds no language for the person, so a save that posts none
    /// must not reach the provisioner and must not fail.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_NoLanguagePostedUnderSsoMode_Succeeds()
    {
        var handler = SsoModeHandler(out var provisioning);

        await handler.HandleAsync(Command(CurrentEmail), CancellationToken.None);

        provisioning.Verify(
            p => p.UpdateProfileAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The point of the whole change: under SSO mode a language hostpanel cannot store is
    /// skipped rather than refused, so the rest of the save still commits. A customer editing
    /// their phone number is not turned away because the form also carried a language.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_LanguagePostedUnderSsoMode_SaveStillSucceeds()
    {
        var handler = SsoModeHandler(out var provisioning);

        // No exception thrown: the provisioner's refusal is caught for this field alone.
        await handler.HandleAsync(Command(CurrentEmail, language: "hy"), CancellationToken.None);

        provisioning.Verify(
            p => p.UpdateProfileAsync(
                Subject, "Roots", "Agency", "hy", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The bug this fixes: a customer changing a billing preference on the account page also
    /// posts their unchanged address, and under SSO mode that made every save fail with a
    /// refusal about sign-in addresses. An unchanged address must not reach the provisioner.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_EmailUnchangedUnderSsoMode_SavesWithoutTouchingProvisioning()
    {
        var handler = SsoModeHandler(out var provisioning);

        // Casing differs on purpose: the same address typed back by a form is the same
        // address, and a case-sensitive test would put this straight back into the refusal.
        await handler.HandleAsync(Command(CurrentEmail.ToUpperInvariant()), CancellationToken.None);

        provisioning.Verify(
            p => p.ChangeEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The other half of the same guard: genuinely moving the account to a different address
    /// is still refused under SSO mode. Making the unchanged case a no-op must not weaken it.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_EmailActuallyChangedUnderSsoMode_IsStillRefused()
    {
        var handler = SsoModeHandler(out _);

        await Assert.ThrowsAsync<UserProvisioningNotAllowedException>(
            () => handler.HandleAsync(Command("someone.else@example.com"), CancellationToken.None));
    }
}
