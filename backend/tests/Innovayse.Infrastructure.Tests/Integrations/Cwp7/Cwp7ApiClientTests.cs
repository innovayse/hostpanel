namespace Innovayse.Infrastructure.Tests.Integrations.Cwp7;

using System.Net;
using System.Text;
using FluentAssertions;
using Innovayse.Providers.CWP7;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

/// <summary>
/// Unit tests for <see cref="Cwp7ApiClient"/>'s response parsing.
/// </summary>
/// <remarks>
/// CWP7 answers errors with HTTP 200 and an HTML body, so <c>EnsureSuccessStatusCode</c> passes.
/// The client used to catch the resulting <c>JsonException</c> and return
/// <c>Status = "OK"</c>, turning a CWP7 error page into a successfully provisioned account —
/// and it logged that at Debug, so at production log levels it recorded nothing at all.
/// </remarks>
public sealed class Cwp7ApiClientTests
{
    /// <summary>A CWP7 error page: HTTP 200, HTML body, no JSON anywhere.</summary>
    private const string ErrorPageHtml =
        """
        <!DOCTYPE html>
        <html><head><title>500 Internal Server Error</title></head>
        <body><h1>Internal Server Error</h1>
        <p>The API key provided is not authorized for this module.</p>
        </body></html>
        """;

    /// <summary>
    /// Builds a client wired to a mock handler returning the given body with HTTP 200.
    /// </summary>
    /// <param name="body">The response body CWP7 answers with.</param>
    /// <param name="mediaType">The Content-Type to answer with.</param>
    /// <returns>The configured client.</returns>
    private static Cwp7ApiClient BuildClient(string body, string mediaType)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            });

        return new Cwp7ApiClient(new HttpClient(mockHandler.Object), NullLogger<Cwp7ApiClient>.Instance);
    }

    [Fact]
    public async Task CreateAccount_Fails_WhenCwp7AnswersWithAnHtmlErrorPageAsync()
    {
        var client = BuildClient(ErrorPageHtml, "text/html");

        var result = await client.CreateAccountAsync(
            "https://cwp7.test:2304", "test-key", "example.com", "testuser", "pass123",
            "default", "test@example.com", "0", "40", "150", "203.0.113.10",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("non-JSON");
    }

    [Fact]
    public async Task SuspendAccount_Fails_WhenCwp7AnswersWithAnHtmlErrorPageAsync()
    {
        var client = BuildClient(ErrorPageHtml, "text/html");

        var result = await client.SuspendAccountAsync(
            "https://cwp7.test:2304", "test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAccount_Succeeds_WhenCwp7AnswersWithTheJsonEnvelopeAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""", "application/json");

        var result = await client.CreateAccountAsync(
            "https://cwp7.test:2304", "test-key", "example.com", "testuser", "pass123",
            "default", "test@example.com", "0", "40", "150", "203.0.113.10",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Account created.");
    }

    /// <summary>
    /// A real <c>/v1/user_session</c> body, captured from a live CWP7 server. Its <c>msj</c> is an
    /// object, unlike every other endpoint's, which is the whole reason this call needs its own
    /// parsing.
    /// </summary>
    private const string UserSessionJson =
        """
        {"status":"OK","msj":{"accounts":1,"details":[{"user":"rootsage",
        "token":"eac44313.f2acd74d.3d04bdcb.1788432569",
        "url":"https://host3.example.com:2083/rootsage/?user_session=eac44313.f2acd74d.3d04bdcb.1788432569"}]}}
        """;

    [Fact]
    public async Task AutoLogin_ReturnsTheSessionUrl_WhenCwp7IssuesOneAsync()
    {
        var client = BuildClient(UserSessionJson, "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            "https://cwp7.test:2304", "test-key", "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(
            "https://host3.example.com:2083/rootsage/?user_session=eac44313.f2acd74d.3d04bdcb.1788432569");
    }

    [Fact]
    public async Task AutoLogin_Fails_WhenTheSessionIsForADifferentUserAsync()
    {
        // A session on somebody else's panel is worse than no session: the button would have
        // opened it and signed this client in as them.
        var client = BuildClient(UserSessionJson, "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            "https://cwp7.test:2304", "test-key", "someoneelse", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AutoLogin_Fails_WhenCwp7RefusesWithAStringMessageAsync()
    {
        // The refusal shape: `msj` is a string here, where a success carries an object. Reading
        // one into the other throws, and that must read as "no session", not as a crash.
        var client = BuildClient(
            """{"status":"Error","msj":"Unauthorized action abc123","format":"JSON"}""",
            "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            "https://cwp7.test:2304", "test-key", "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized action");
    }

    [Fact]
    public async Task AutoLogin_Fails_WhenTheEndpointIsNotServedAsync()
    {
        // What every client actually got until this was fixed: the call named a route CWP7 does
        // not serve, and its 404 page came back with HTTP 200.
        var client = BuildClient(
            "<html><head><title>404 Page Not Found</title></head><body></body></html>",
            "text/html");

        var result = await client.GetAutoLoginUrlAsync(
            "https://cwp7.test:2304", "test-key", "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AutoLogin_CallsTheUserSessionRouteAsync()
    {
        // The bug was the address, not the parsing, so the address is what this pins down.
        var mockHandler = new Mock<HttpMessageHandler>();
        HttpRequestMessage? sent = null;

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => sent = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UserSessionJson, Encoding.UTF8, "application/json"),
            });

        var client = new Cwp7ApiClient(
            new HttpClient(mockHandler.Object), NullLogger<Cwp7ApiClient>.Instance);

        await client.GetAutoLoginUrlAsync(
            "https://cwp7.test:2304", "test-key", "rootsage", CancellationToken.None);

        sent!.RequestUri!.AbsolutePath.Should().Be("/v1/user_session");
    }
}
