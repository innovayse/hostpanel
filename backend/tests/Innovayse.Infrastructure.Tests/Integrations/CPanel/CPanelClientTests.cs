namespace Innovayse.Infrastructure.Tests.Integrations.CPanel;

using System.Net;
using System.Text;
using FluentAssertions;
using Innovayse.Infrastructure.Integrations.CPanel;
using Innovayse.Infrastructure.Integrations.CPanel.Options;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CPanelClient"/>'s WHM JSON API v1 envelope handling.
/// </summary>
/// <remarks>
/// The client asks for <c>api.version=1</c>, whose refusals arrive as HTTP 200 with
/// <c>{"metadata":{"result":0,"reason":"…"}}</c>. It used to look for the API v0
/// <c>result.status</c> / <c>statusmsg</c> pair, which a v1 body never carries, so every WHM
/// refusal returned as a success and all seven functions were unable to report failure.
/// </remarks>
public sealed class CPanelClientTests
{
    /// <summary>Settings pointing the client at a WHM host that does not exist.</summary>
    private static CPanelOptions Settings => new()
    {
        ApiUrl = "https://whm.test:2087",
        Username = "root",
        ApiToken = "test-token",
        ServerIp = "203.0.113.10",
    };

    /// <summary>
    /// Builds a client wired to a mock handler returning <paramref name="responseJson"/>,
    /// and captures the last request URL so tests can assert on the query parameters sent.
    /// </summary>
    /// <param name="responseJson">The JSON body WHM answers with.</param>
    /// <returns>The client and a reader for the last request URL.</returns>
    private static (CPanelClient Client, Func<string> LastRequestUrl) BuildClient(string responseJson)
    {
        string? lastUrl = null;

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => lastUrl = req.RequestUri!.ToString())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });

        var http = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri(Settings.ApiUrl),
        };

        return (new CPanelClient(http, Options.Create(Settings)), () => lastUrl ?? string.Empty);
    }

    /// <summary>A real WHM v1 refusal: HTTP 200, result 0, reason in the metadata.</summary>
    private const string CreateAcctRefused =
        """
        {
          "metadata": {
            "command": "createacct",
            "reason": "The account 'testuser' already exists.",
            "result": 0,
            "version": 1
          },
          "data": {}
        }
        """;

    /// <summary>A real WHM v1 success envelope.</summary>
    private const string CreateAcctAccepted =
        """
        {
          "metadata": {
            "command": "createacct",
            "reason": "Account Creation Ok",
            "result": 1,
            "version": 1
          },
          "data": { "ip": "203.0.113.10", "nameserver": "ns1.test" }
        }
        """;

    [Fact]
    public async Task CreateAccount_Throws_WhenWhmMetadataResultIsZeroAsync()
    {
        var (client, _) = BuildClient(CreateAcctRefused);

        var act = async () => await client.CreateAccountAsync(
            "example.com", "testuser", "pass123", "default", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*createacct*")
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAccount_DoesNotThrow_WhenWhmMetadataResultIsOneAsync()
    {
        var (client, _) = BuildClient(CreateAcctAccepted);

        var act = async () => await client.CreateAccountAsync(
            "example.com", "testuser", "pass123", "default", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SuspendAccount_Throws_WhenWhmRefusesAsync()
    {
        var (client, _) = BuildClient(
            """{ "metadata": { "result": 0, "reason": "Access denied", "version": 1 } }""");

        var act = async () => await client.SuspendAccountAsync("testuser", "non-payment", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Access denied*");
    }

    [Fact]
    public async Task CallApi_Throws_WhenResponseCarriesNoMetadataEnvelopeAsync()
    {
        // The old API v0 shape. It is not a v1 success, and must not be read as one.
        var (client, _) = BuildClient("""{ "result": { "status": 1, "statusmsg": "ok" } }""");

        var act = async () => await client.UnsuspendAccountAsync("testuser", CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*metadata.result*");
    }

    [Fact]
    public async Task RemoveAccount_SendsTheUserParameterWhmActuallyReadsAsync()
    {
        var (client, lastUrl) = BuildClient(
            """{ "metadata": { "result": 1, "reason": "Ok", "version": 1 } }""");

        await client.RemoveAccountAsync("testuser", CancellationToken.None);

        // WHM's removeacct names the account "user". "username" is ignored and the account survives.
        lastUrl().Should().Contain("user=testuser");
        lastUrl().Should().NotContain("username=");
    }
}
