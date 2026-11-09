using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Auth;

/// <summary>
/// The local-auth controller's <c>/api/auth/*</c> routes: register and login succeed with the
/// documented bodies, and every refusal answers in the product's one error shape: problem+json,
/// the code in <c>type</c>, the documented status, and a <c>detail</c> in the request's
/// language.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LocalAuthControllerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A password that satisfies the local password policy.</summary>
    private const string Password = "Password123!";

    /// <summary>A wrong password is a 401 with the sign-in code and a Russian sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LoginWithWrongPassword_Refuses401WithLocalizedDetail()
    {
        var client = RussianClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword!" });

        await AssertRefusalAsync(response, HttpStatusCode.Unauthorized, HostpanelErrorCodes.SIGN_IN_UNAUTHORIZED);
    }

    /// <summary>An empty body is a 400 validation response, not a 500 from a null argument.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LoginWithEmptyBody_Refuses400WithValidationErrors()
    {
        var client = RussianClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(SharedErrorCodes.ValidationError);
        FieldsOf(body).Should().BeEquivalentTo("email", "password");
    }

    /// <summary>A body without a password is a 400 naming only the password, in the request's language.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LoginWithMissingPassword_Refuses400NamingPassword()
    {
        var client = RussianClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "someone@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        FieldsOf(body).Should().BeEquivalentTo("password");
        body.RootElement.GetProperty("errors").EnumerateObject().Single().Value[0].GetString()
            .Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>A pending token that is not base64 is a 401 with the pending-token code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoFactorLoginWithMalformedPendingToken_Refuses401()
    {
        var client = RussianClient();

        var response = await client.PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = "%%%", code = "000000" });

        await AssertRefusalAsync(response, HttpStatusCode.Unauthorized, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>
    /// An empty pending token, which the admin panel sends once its in-memory token is lost, is a 401
    /// with the pending-token code and a localized detail, not a 400 from model binding or a 500.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoFactorLoginWithEmptyPendingToken_Refuses401WithLocalizedDetail()
    {
        var client = RussianClient();

        var response = await client.PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken = "", code = "000000" });

        await AssertRefusalAsync(response, HttpStatusCode.Unauthorized, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A body with no pending token at all gets the same 401 as an empty one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoFactorLoginWithMissingPendingToken_Refuses401WithLocalizedDetail()
    {
        var client = RussianClient();

        var response = await client.PostAsJsonAsync("/api/auth/2fa-login", new { code = "000000" });

        await AssertRefusalAsync(response, HttpStatusCode.Unauthorized, HostpanelErrorCodes.PENDING_TOKEN_UNAUTHORIZED);
    }

    /// <summary>A well-formed pending token with the wrong code is a 401 with the code-mismatch code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoFactorLoginWithWrongCode_Refuses401()
    {
        using var two = new TwoFactorHost(factory);
        var account = two.CreateClient();
        var email = $"refusal-{Guid.NewGuid():N}@example.com";
        (await account.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Re", lastName = "Fusal" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var loggedIn = await account.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        using var session = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        account.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", session.RootElement.GetProperty("accessToken").GetString()!);
        var secret = await AuthTestSeeds.StartSetupAsync(account);
        (await account.PostAsJsonAsync("/api/me/2fa/enable", new { code = two.PreviousCode(secret) }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await two.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        using var challenge = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var pendingToken = challenge.RootElement.GetProperty("pendingToken").GetString()!;
        var client = two.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.PostAsJsonAsync("/api/auth/2fa-login", new { pendingToken, code = "000000" });

        await AssertRefusalAsync(response, HttpStatusCode.Unauthorized, HostpanelErrorCodes.TWO_FACTOR_CODE_UNAUTHORIZED);
    }

    /// <summary>Registering an address that already has an account is a 400 with the registration code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDuplicateEmail_Refuses400()
    {
        var client = RussianClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Dup", lastName = "Licate" });

        await AssertRefusalAsync(response, HttpStatusCode.BadRequest, HostpanelErrorCodes.REGISTRATION_FAILED);
    }

    /// <summary>An unknown reset token is a 400 with the token code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResetPasswordWithBadToken_Refuses400()
    {
        var client = RussianClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { email, token = "not-a-token", newPassword = "Another123!" });

        await AssertRefusalAsync(response, HttpStatusCode.BadRequest, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }

    /// <summary>An unknown confirmation token is a 400 with the token code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConfirmEmailWithBadToken_Refuses400()
    {
        var client = RussianClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new { email, token = "not-a-token" });

        await AssertRefusalAsync(response, HttpStatusCode.BadRequest, HostpanelErrorCodes.TOKEN_INVALID_OR_EXPIRED);
    }

    /// <summary>The same refusal in English when no language is asked for.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Login_WithoutLanguageHeader_AnswersInEnglish()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = $"nobody-{Guid.NewGuid():N}@example.com", password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("detail").GetString().Should().Be("Invalid email or password.");
    }

    /// <summary>The field names a validation response lists, lower-cased so the key policy does not matter.</summary>
    /// <param name="body">The parsed problem+json body.</param>
    /// <returns>The lower-cased property names under <c>errors</c>.</returns>
    private static string[] FieldsOf(JsonDocument body)
    {
        return [.. body.RootElement.GetProperty("errors").EnumerateObject().Select(f => f.Name.ToLowerInvariant())];
    }

    /// <summary>A client that asks for Russian.</summary>
    /// <returns>An unauthenticated client with <c>X-Language: ru</c>.</returns>
    private HttpClient RussianClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");
        return client;
    }

    /// <summary>Registers a brand-new local account.</summary>
    /// <param name="client">The client to register with.</param>
    /// <returns>The new account's email address.</returns>
    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"refusal-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Re", lastName = "Fusal" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return email;
    }

    /// <summary>Asserts the whole error contract: media type, status, code, Russian detail, and no member beyond the error shape.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected error code.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private static async Task AssertRefusalAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(code);
        body.RootElement.GetProperty("status").GetInt32().Should().Be((int)status);
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
        body.RootElement.TryGetProperty("error", out _).Should().BeFalse();
    }

    /// <summary>
    /// Register with valid data returns 200 and the new user's id.
    /// </summary>
    /// <remarks>
    /// Registration creates the account and does not sign the caller in: a token comes from
    /// /api/auth/login like it does for everyone else. The login case below covers the token.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Register_WithValidData_Returns200WithUserIdAsync()
    {
        var client = factory.CreateClient();
        var body = new
        {
            email = $"test-{Guid.NewGuid()}@example.com",
            password = "Password123!",
            firstName = "John",
            lastName = "Doe"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("userId");
    }

    /// <summary>Login with valid credentials returns 200 with access token.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithAccessTokenAsync()
    {
        var client = factory.CreateClient();
        var email = $"login-test-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.Should().Contain("accessToken");
    }

    /// <summary>Login with wrong password returns non-200 status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Login_WithWrongPassword_ReturnsErrorAsync()
    {
        var client = factory.CreateClient();
        var email = $"wrong-pwd-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password123!",
            firstName = "Jane",
            lastName = "Doe"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
