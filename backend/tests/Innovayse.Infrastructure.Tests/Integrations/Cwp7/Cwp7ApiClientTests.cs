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
}
