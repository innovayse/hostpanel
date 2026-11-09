using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The error body on the surfaces that answer before a controller runs: model binding and the
/// authentication challenge. Each must be an <c>application/problem+json</c> <c>ErrorResponse</c>,
/// and must carry nothing beyond the standard members.
/// </summary>
/// <param name="factory">The shared test host.</param>
public sealed class ErrorBodyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An action that needs a signed-in administrator.</summary>
    private const string AdminOnlyPath = "/api/admin/users";

    /// <summary>An anonymous action that binds a JSON body.</summary>
    private const string LoginPath = "/api/auth/login";

    /// <summary>The RFC 9457 media type every error body carries.</summary>
    private const string ProblemJson = "application/problem+json";

    /// <summary>
    /// Reads the body as JSON after asserting the status and media type every error body shares.
    /// </summary>
    /// <param name="response">The response under test.</param>
    /// <param name="status">The status it must carry.</param>
    /// <returns>The parsed body.</returns>
    private static async Task<JsonElement> ReadErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.Clone();

        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        return body;
    }

    /// <summary>
    /// Posts a body that is not JSON to an anonymous action.
    /// </summary>
    /// <param name="client">The client to post with.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PostMalformedLoginAsync(HttpClient client)
    {
        var content = new StringContent("{ not json", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return client.PostAsync(LoginPath, content);
    }

    /// <summary>
    /// A body that does not parse answers 400 VALIDATION_ERROR.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MalformedBody_Answers400ValidationErrorAsync()
    {
        var client = factory.CreateClient();

        var body = await ReadErrorAsync(await PostMalformedLoginAsync(client), HttpStatusCode.BadRequest);

        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A binder failure never puts a CLR type name on the wire.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MalformedBody_LeaksNoInternalTypeNameAsync()
    {
        var client = factory.CreateClient();

        var response = await PostMalformedLoginAsync(client);
        var text = await response.Content.ReadAsStringAsync();

        text.Should().NotContain("Innovayse.");
        text.Should().NotContain("System.");
    }

    /// <summary>No session on an admin action answers 401 UNAUTHORIZED with a body, not an empty status line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoSession_Answers401WithBodyAsync()
    {
        var client = factory.CreateClient();

        var body = await ReadErrorAsync(await client.GetAsync(AdminOnlyPath), HttpStatusCode.Unauthorized);

        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.Unauthorized);
    }

    /// <summary>
    /// The detail follows the request's X-Language: Russian asked, Russian answered.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoSession_WithRussian_AnswersARussianDetailAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var body = await ReadErrorAsync(await client.GetAsync(AdminOnlyPath), HttpStatusCode.Unauthorized);

        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>
    /// X-Language is the only culture source: an Accept-Language header alone is ignored and the
    /// detail stays in the default language.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoSession_WithAcceptLanguageOnly_AnswersTheDefaultLanguageAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "ru");

        var body = await ReadErrorAsync(await client.GetAsync(AdminOnlyPath), HttpStatusCode.Unauthorized);

        body.GetProperty("detail").GetString().Should().NotMatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>The body is the bare ErrorResponse: no legacy <c>error</c> and no <c>code</c> member.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ErrorBody_CarriesOnlyTheStandardMembersAsync()
    {
        var client = factory.CreateClient();

        var body = await ReadErrorAsync(await client.GetAsync(AdminOnlyPath), HttpStatusCode.Unauthorized);

        body.TryGetProperty("error", out _).Should().BeFalse();
        body.TryGetProperty("code", out _).Should().BeFalse();
        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.Unauthorized);
    }
}
