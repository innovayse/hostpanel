using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>POST /api/clients/me/email-change</c>: a client asks to move their sign-in to a new address.
/// The password is checked, a link goes to the new address, and nothing changes yet.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartMyEmailChangeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// With the right password a link is mailed to the new address only, and the account keeps
    /// signing in with the old one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RightPassword_MailsTheNewAddressAndChangesNothingYet()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail, currentPassword = EmailChangeSeeds.Password });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var link = EmailChangeSeeds.LinkTo(account.Sender, newEmail);
        link.AbsolutePath.Should().Be("/client/confirm-email-change");
        EmailChangeSeeds.QueryOf(link, "email").Should().Be(newEmail);
        EmailChangeSeeds.QueryOf(link, "token").Should().NotBeNullOrWhiteSpace();
        // The old address did get the verification mail its registration sent; what must not reach
        // it is the link that moves the account.
        account.Sender.Sent.Should().NotContain(
            m => m.To == account.Email && m.Body.Contains("confirm-email-change", StringComparison.Ordinal));
        (await EmailChangeSeeds.SignInStatusAsync(account, account.Email)).Should().Be(HttpStatusCode.OK);
        (await EmailChangeSeeds.SignInStatusAsync(account, newEmail)).Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A wrong password is refused and nothing is mailed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongPassword_IsRefusedAndMailsNothing()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail, currentPassword = "Not-The-Password-1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CURRENT_PASSWORD_INVALID);
        account.Sender.Sent.Should().NotContain(m => m.To == newEmail);
    }

    /// <summary>An address that already belongs to an account is refused with a conflict.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddressInUse_IsRefused()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var other = await EmailChangeSeeds.SignedInClientAsync(factory);

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail = other.Email, currentPassword = EmailChangeSeeds.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
    }

    /// <summary>A malformed address or a missing password fails validation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MalformedBody_IsRefusedByValidation()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail = "not-an-address", currentPassword = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Anonymous_IsRefused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail = "someone@example.test", currentPassword = "whatever" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
