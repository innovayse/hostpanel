using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// <c>POST /api/clients/me/email-change/confirm</c>: the token mailed to the new address makes the
/// change effective, ends every earlier session and tells the old address.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConfirmMyEmailChangeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The mailed token moves the sign-in to the new address, the old address is told, and the
    /// session that confirmed it has ended.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MailedToken_MovesTheSignInAndEndsEarlierSessions()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";
        (await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail, currentPassword = EmailChangeSeeds.Password }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var link = EmailChangeSeeds.LinkTo(account.Sender, newEmail);

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.ConfirmRoute,
            new { email = EmailChangeSeeds.QueryOf(link, "email"), token = EmailChangeSeeds.QueryOf(link, "token") });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EmailChangeSeeds.SignInStatusAsync(account, newEmail)).Should().Be(HttpStatusCode.OK);
        (await EmailChangeSeeds.SignInStatusAsync(account, account.Email)).Should().Be(HttpStatusCode.Unauthorized);
        account.Sender.Sent.Should().Contain(m => m.To == account.Email && m.Body.Contains(newEmail));
        (await account.Client.GetAsync("/api/clients/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A token that was never issued is refused and the address stays as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongToken_IsRefusedAndChangesNothing()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.ConfirmRoute, new { email = newEmail, token = "not-a-token" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
        (await EmailChangeSeeds.SignInStatusAsync(account, account.Email)).Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A token issued for one address does not confirm another.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TokenForAnotherAddress_IsRefused()
    {
        var account = await EmailChangeSeeds.SignedInClientAsync(factory);
        var newEmail = $"moved-{Guid.NewGuid():N}@example.test";
        (await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.StartRoute, new { newEmail, currentPassword = EmailChangeSeeds.Password }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var token = EmailChangeSeeds.QueryOf(EmailChangeSeeds.LinkTo(account.Sender, newEmail), "token");

        var response = await account.Client.PostAsJsonAsync(
            EmailChangeSeeds.ConfirmRoute, new { email = $"elsewhere-{Guid.NewGuid():N}@example.test", token });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Anonymous_IsRefused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            EmailChangeSeeds.ConfirmRoute, new { email = "someone@example.test", token = "whatever" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
