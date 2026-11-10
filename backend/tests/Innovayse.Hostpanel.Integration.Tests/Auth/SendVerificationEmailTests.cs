using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The verification mail a local registration sends, and the confirmation round trip it enables.
/// The one port this platform does not own, the SMTP relay, is a <see cref="FakeEmailSender"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SendVerificationEmailTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Registration sends exactly one verification mail, and its link points at the client portal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_SendsExactlyOneMailWithAWorkingLinkAsync()
    {
        var sender = new FakeEmailSender();
        var client = VerificationMailSeeds.ClientWithRelay(factory, sender);

        var email = await VerificationMailSeeds.RegisterAsync(client);

        var mail = VerificationMailSeeds.VerificationMailsTo(sender, email).Should().ContainSingle().Subject;
        var link = VerificationMailSeeds.LinkOf(mail);
        link.AbsolutePath.Should().Be("/client/confirm-email");
        VerificationMailSeeds.QueryOf(link, "email").Should().Be(email);
        VerificationMailSeeds.QueryOf(link, "token").Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>The token from the mailed link confirms the address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConfirmEmail_WithTheMailedToken_MarksTheAddressVerifiedAsync()
    {
        var sender = new FakeEmailSender();
        var client = VerificationMailSeeds.ClientWithRelay(factory, sender);
        var email = await VerificationMailSeeds.RegisterAsync(client);
        await VerificationMailSeeds.SignInAsync(client, email);
        (await VerificationMailSeeds.IsVerifiedAsync(client)).Should().BeFalse();
        var link = VerificationMailSeeds.LinkOf(VerificationMailSeeds.VerificationMailsTo(sender, email).Single());

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { email = VerificationMailSeeds.QueryOf(link, "email"), token = VerificationMailSeeds.QueryOf(link, "token") });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await VerificationMailSeeds.IsVerifiedAsync(client)).Should().BeTrue();
    }

    /// <summary>A token that was never issued is refused and confirms nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConfirmEmail_WithABadToken_IsRefusedAndLeavesTheAddressUnverifiedAsync()
    {
        var sender = new FakeEmailSender();
        var client = VerificationMailSeeds.ClientWithRelay(factory, sender);
        var email = await VerificationMailSeeds.RegisterAsync(client);
        await VerificationMailSeeds.SignInAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new { email, token = "not-a-token" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
        (await VerificationMailSeeds.IsVerifiedAsync(client)).Should().BeFalse();
    }
}
