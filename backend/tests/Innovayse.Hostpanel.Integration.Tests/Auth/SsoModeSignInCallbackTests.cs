#if INNOVAYSE_SSO
// Only compiled when the optional sign-on package is referenced (the InnovayseSso property in
// backend/Directory.Build.props): these tests exercise the sign-in endpoints that package maps.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The admin sign-in callback the sign-on client package maps, as a browser meets it: a callback whose
/// sign-in attempt is gone (a stale tab, Back after signing in, a replay) redirects or renders a small page and
/// never answers a JSON body, and two attempts started in one browser do not spend each other.
/// </summary>
/// <remarks>
/// The tests stop before the code exchange, so no provider is contacted: an attempt that is found and
/// carries no authorization code ends on the refusal page, which is enough to tell "this attempt was found
/// and spent" from "this attempt was never there". Redis is real because the package protects the
/// attempt cookie with a key ring kept in it.
/// </remarks>
public sealed class SsoModeSignInCallbackTests : IAsyncLifetime
{
    /// <summary>Where the package sends a browser that has no sign-in attempt, once.</summary>
    private const string RestartPath = "/api/auth/login?restart=1";

    /// <summary>The package's sign-in start address.</summary>
    private const string LoginPath = "/api/auth/login";

    /// <summary>The package's callback address.</summary>
    private const string CallbackPath = "/api/auth/callback";

    /// <summary>Throw-away database the host under test connects to.</summary>
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>Redis holding the sign-on client package's key ring.</summary>
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>Host booted in single sign-on mode; null until <see cref="InitializeAsync"/> has run.</summary>
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Starts the database and Redis containers and builds the host in single sign-on mode.</summary>
    /// <returns>A task that completes when the host is built.</returns>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();

        _factory = new SsoModeFactory(_postgres.GetConnectionString(), _redis.GetConnectionString());
        _ = _factory.Services;
    }

    /// <summary>Disposes the host and both containers.</summary>
    /// <returns>A task that completes when everything is released.</returns>
    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>A callback for an attempt this browser does not hold restarts the sign-in once, with nothing cacheable.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Callback_UnknownState_RedirectsToARestartedSignIn()
    {
        using var client = CreateSecureClient();

        using var response = await client.GetAsync($"{CallbackPath}?code=stale&state=unknown");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be(RestartPath);
        response.Headers.CacheControl?.ToString().Should().Contain("no-store");
    }

    /// <summary>A restarted sign-in that finds no attempt either ends on an HTML page with a link back, never on JSON.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Callback_UnknownRestartedState_AnswersAnHtmlPageNotJson()
    {
        using var client = CreateSecureClient();

        using var response = await client.GetAsync($"{CallbackPath}?code=stale&state=unknown.r");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain(LoginPath);
    }

    /// <summary>A callback with no state at all is the same unknown attempt, not a malformed-request body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Callback_NoState_RedirectsToARestartedSignIn()
    {
        using var client = CreateSecureClient();

        using var response = await client.GetAsync($"{CallbackPath}?code=stale");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be(RestartPath);
    }

    /// <summary>
    /// The first callback of an attempt spends it; a repeat of the same callback (Back after sign-in, a second
    /// tab) finds nothing and redirects instead of answering a 400 body.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Callback_RepeatedState_RedirectsInsteadOfRefusing()
    {
        using var client = CreateSecureClient();
        var attempt = await StartAttemptAsync(client);

        using var first = await CallbackAsync(client, attempt, Header(attempt.Cookie));
        first.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the attempt was found and spent, and no code came with it");
        first.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        // The browser deleted the attempt cookie the first response told it to, so the repeat carries none.
        using var repeat = await CallbackAsync(client, attempt, cookieHeader: null);

        repeat.StatusCode.Should().Be(HttpStatusCode.Redirect);
        repeat.Headers.Location?.OriginalString.Should().Be(RestartPath);
    }

    /// <summary>
    /// Two sign-ins started in one browser keep a cookie each, and finishing one leaves the other usable.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoParallelAttempts_KeepSeparateCookies_AndSpendOnlyTheirOwn()
    {
        using var client = CreateSecureClient();
        var first = await StartAttemptAsync(client);
        var second = await StartAttemptAsync(client);

        first.State.Should().NotBe(second.State);
        first.Cookie.Name.Should().NotBe(second.Cookie.Name, "one cookie per attempt is what stops the second sign-in overwriting the first");

        var bothCookies = $"{Header(first.Cookie)}; {Header(second.Cookie)}";

        using var spendFirst = await CallbackAsync(client, first, bothCookies);
        spendFirst.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the first attempt was found");
        DeletedCookieNames(spendFirst).Should().Equal(first.Cookie.Name);

        // Only the cookie the first response did not delete is still in the browser.
        using var spendSecond = await CallbackAsync(client, second, Header(second.Cookie));
        spendSecond.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the second attempt survived the first one finishing");
        spendSecond.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        // And the first attempt really is spent: its callback no longer finds anything.
        using var replayFirst = await CallbackAsync(client, first, cookieHeader: null);
        replayFirst.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    /// <summary>Builds a client that talks HTTPS, which the sign-in endpoints require for their <c>__Host-</c> cookies, and follows no redirect.</summary>
    /// <returns>A client addressed to a secure origin.</returns>
    private HttpClient CreateSecureClient()
    {
        return _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    /// <summary>Starts a sign-in the way a browser does and reads back the state it carries and the attempt cookie it was given.</summary>
    /// <param name="client">The secure client.</param>
    /// <returns>The attempt's state and its cookie.</returns>
    private static async Task<SignInAttempt> StartAttemptAsync(HttpClient client)
    {
        using var response = await client.GetAsync(LoginPath);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        var state = query["state"].ToString();
        state.Should().NotBeNullOrEmpty();

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Select(ParseCookie)
            .Single(c => c.Value.Length > 0);

        return new SignInAttempt(state, cookie);
    }

    /// <summary>Sends the provider's return to the callback for one attempt, without a code.</summary>
    /// <param name="client">The secure client.</param>
    /// <param name="attempt">The attempt whose state comes back.</param>
    /// <param name="cookieHeader">The whole <c>Cookie</c> header the browser sends, or <see langword="null"/> for none.</param>
    /// <returns>The callback's response.</returns>
    private static async Task<HttpResponseMessage> CallbackAsync(HttpClient client, SignInAttempt attempt, string? cookieHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{CallbackPath}?state={Uri.EscapeDataString(attempt.State)}");
        if (cookieHeader is not null)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Writes one cookie the way a browser sends it back.</summary>
    /// <param name="cookie">The cookie.</param>
    /// <returns>The <c>name=value</c> pair.</returns>
    private static string Header(SignInCookie cookie)
    {
        return $"{cookie.Name}={cookie.Value}";
    }

    /// <summary>Names the cookies a response tells the browser to delete.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The names of the cookies set with an empty value or an expiry in the past.</returns>
    private static IEnumerable<string> DeletedCookieNames(HttpResponseMessage response)
    {
        return response.Headers.GetValues("Set-Cookie")
            .Where(h => ParseCookie(h).Value.Length == 0)
            .Select(h => ParseCookie(h).Name);
    }

    /// <summary>Reads the name and value off a <c>Set-Cookie</c> header.</summary>
    /// <param name="header">The header value.</param>
    /// <returns>The cookie's name and value.</returns>
    private static SignInCookie ParseCookie(string header)
    {
        var pair = header.Split(';', 2)[0];
        var separator = pair.IndexOf('=');
        return new SignInCookie(pair[..separator], pair[(separator + 1)..]);
    }

    /// <summary>A cookie's name and value as a browser would send it back.</summary>
    /// <param name="Name">The cookie name.</param>
    /// <param name="Value">The cookie value.</param>
    private sealed record SignInCookie(string Name, string Value);

    /// <summary>One started sign-in: the state the provider will return, and the cookie that holds the attempt.</summary>
    /// <param name="State">The state parameter of the redirect to the provider.</param>
    /// <param name="Cookie">The attempt cookie set with that redirect.</param>
    private sealed record SignInAttempt(string State, SignInCookie Cookie);
}

#endif
