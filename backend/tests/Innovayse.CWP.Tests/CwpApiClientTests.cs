namespace Innovayse.CWP.Tests;

using System.Net;
using System.Text;
using FluentAssertions;
using Innovayse.Providers.CWP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

/// <summary>Unit tests for <see cref="CwpApiClient"/> using a mocked HTTP message handler.</summary>
public sealed class CwpApiClientTests
{
    /// <summary>
    /// Creates a <see cref="CwpApiClient"/> wired to a mock handler that returns the given JSON body.
    /// </summary>
    /// <param name="responseBody">JSON string to return as the HTTP response body.</param>
    /// <param name="statusCode">HTTP status code to return. Default 200 OK.</param>
    /// <returns>Configured <see cref="CwpApiClient"/> pointing to "cwp.test:2031".</returns>
    private static CwpApiClient BuildClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });

        var http = new HttpClient(mockHandler.Object);
        return new CwpApiClient(http, NullLogger<CwpApiClient>.Instance);
    }

    /// <summary>CreateAccountAsync should return IsSuccess=true when CWP responds with status "OK".</summary>
    [Fact]
    public async Task CreateAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""");

        var result = await client.CreateAccountAsync(
            "https://cwp.test:2304", "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Account created.");
    }

    /// <summary>CreateAccountAsync should return IsSuccess=false when CWP responds with status "Error".</summary>
    [Fact]
    public async Task CreateAccount_ReturnsFalse_WhenCwpRespondsErrorAsync()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "User already exists." }""");

        var result = await client.CreateAccountAsync(
            "https://cwp.test:2304", "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User already exists.");
    }

    /// <summary>SuspendAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task SuspendAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account suspended." }""");

        var result = await client.SuspendAccountAsync("https://cwp.test:2304", "test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>UnsuspendAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task UnsuspendAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account unsuspended." }""");

        var result = await client.UnsuspendAccountAsync("https://cwp.test:2304", "test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>TerminateAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task TerminateAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account deleted." }""");

        var result = await client.TerminateAccountAsync("https://cwp.test:2304", "test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>SendAsync should throw HttpRequestException when the server returns 500.</summary>
    [Fact]
    public async Task CreateAccount_Throws_WhenServerReturns500Async()
    {
        var client = BuildClient("Internal Server Error", HttpStatusCode.InternalServerError);

        var act = () => client.CreateAccountAsync(
            "https://cwp.test:2304", "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
