using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// <c>POST /api/auth/resend-verification</c>: sends the caller a fresh verification mail, and does
/// nothing once the address is confirmed. The SMTP relay is a <see cref="FakeEmailSender"/>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ResendVerificationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An unverified caller gets a second mail whose link confirms the address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Resend_ForAnUnverifiedCaller_SendsAWorkingLinkAsync()
    {
        var sender = new FakeEmailSender();
        var client = VerificationMailSeeds.ClientWithRelay(factory, sender);
        var email = await VerificationMailSeeds.RegisterAsync(client);
        await VerificationMailSeeds.SignInAsync(client, email);

        var response = await client.PostAsync("/api/auth/resend-verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var mails = VerificationMailSeeds.VerificationMailsTo(sender, email);
        mails.Should().HaveCount(2);
        var link = VerificationMailSeeds.LinkOf(mails[^1]);
        var confirmed = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { email = VerificationMailSeeds.QueryOf(link, "email"), token = VerificationMailSeeds.QueryOf(link, "token") });
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await VerificationMailSeeds.IsVerifiedAsync(client)).Should().BeTrue();
    }

    /// <summary>Once the address is confirmed, resending answers success and sends nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Resend_ForAVerifiedCaller_IsANoOpAsync()
    {
        var sender = new FakeEmailSender();
        var client = VerificationMailSeeds.ClientWithRelay(factory, sender);
        var email = await VerificationMailSeeds.RegisterAsync(client);
        await VerificationMailSeeds.SignInAsync(client, email);
        var link = VerificationMailSeeds.LinkOf(VerificationMailSeeds.VerificationMailsTo(sender, email).Single());
        (await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { email = VerificationMailSeeds.QueryOf(link, "email"), token = VerificationMailSeeds.QueryOf(link, "token") }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync("/api/auth/resend-verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        VerificationMailSeeds.VerificationMailsTo(sender, email).Should().HaveCount(1);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Resend_WithoutASession_Refuses401Async()
    {
        var client = VerificationMailSeeds.ClientWithRelay(factory, new FakeEmailSender());

        var response = await client.PostAsync("/api/auth/resend-verification", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
