using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.IntegrationEvents.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared plumbing for the sign-in address change tests: a client account on a host whose mail
/// relay is a <see cref="FakeEmailSender"/>, and the link read back out of the mail sent to the new address.
/// </summary>
public static class EmailChangeSeeds
{
    /// <summary>The password every account this class registers signs in with.</summary>
    public const string Password = "Password123!";

    /// <summary>The route that starts an address change.</summary>
    public const string StartRoute = "/api/clients/me/email-change";

    /// <summary>The route that makes an address change effective.</summary>
    public const string ConfirmRoute = "/api/clients/me/email-change/confirm";

    /// <summary>Matches the link behind the confirmation button.</summary>
    private static readonly Regex LinkPattern = new("href=\"([^\"]*confirm-email-change[^\"]*)\"", RegexOptions.Compiled);

    /// <summary>
    /// Registers a client account on a host with a fake relay, makes sure its client record exists
    /// and signs it in.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>The signed-in account.</returns>
    public static async Task<EmailChangeAccount> SignedInClientAsync(IntegrationTestFactory factory)
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        var client = host.CreateClient();
        var email = $"email-change-{Guid.NewGuid():N}@example.test";

        var registered = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Address", lastName = "Change" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        // The client record is written by a queued handler; publishing the same event again and
        // waiting for it makes the row exist now. The handler skips a client that already exists and
        // treats a concurrent insert by the queued copy as done, so the second delivery is harmless.
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.PublishAndWaitAsync(new ClientRegisteredIntegrationEvent(userId, email, "Address", "Change"));

            (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Clients
                .AnyAsync(c => c.UserId == userId)).Should().BeTrue();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client, email));
        return new EmailChangeAccount(client, host, email, sender);
    }

    /// <summary>Signs in and returns the access token.</summary>
    /// <param name="client">Any client of the host.</param>
    /// <param name="email">The address to sign in with.</param>
    /// <returns>The access token.</returns>
    public static async Task<string> SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    /// <summary>Asks whether a sign-in with the address and the shared password is accepted.</summary>
    /// <param name="account">The account whose host to ask.</param>
    /// <param name="email">The address to try.</param>
    /// <returns>The sign-in status.</returns>
    public static async Task<HttpStatusCode> SignInStatusAsync(EmailChangeAccount account, string email)
    {
        var response = await account.Host.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        return response.StatusCode;
    }

    /// <summary>The confirmation link in the mail sent to an address.</summary>
    /// <param name="sender">The fake relay that recorded the mail.</param>
    /// <param name="email">The new address.</param>
    /// <returns>The link, HTML-decoded, as a browser would follow it.</returns>
    public static Uri LinkTo(FakeEmailSender sender, string email)
    {
        var mail = sender.Sent.Where(m => m.To == email && LinkPattern.IsMatch(m.Body)).Should().ContainSingle().Subject;
        return new Uri(WebUtility.HtmlDecode(LinkPattern.Match(mail.Body).Groups[1].Value));
    }

    /// <summary>Reads one query parameter of a link, URL-decoded.</summary>
    /// <param name="link">The confirmation link.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The decoded value, or <see langword="null"/> when the link has none.</returns>
    public static string? QueryOf(Uri link, string name) => HttpUtility.ParseQueryString(link.Query)[name];
}
