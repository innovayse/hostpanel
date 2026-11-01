using System.Net;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Providers.CWP;
using Innovayse.Providers.CWP.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.CWP.Tests;

/// <summary>Unit tests for <see cref="CwpApiClient"/> against a stub CWP HTTP server.</summary>
public sealed class CwpApiClientTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the CWP host; started per test class instance.</summary>
    private StubHttpServer cwp = null!;

    /// <summary>Gets the stub server address without the trailing slash, as the client expects a base URL.</summary>
    private string BaseUrl => cwp.BaseAddress.ToString().TrimEnd('/');

    /// <summary>Gets the connection aimed at the stub server.</summary>
    private CwpConnection Connection => new(BaseUrl, "test-key");

    /// <summary>Gets the account every create test asks for.</summary>
    private static CwpNewAccount NewAccount =>
        new("example.com", "testuser", "pass123", "default", "test@example.com");

    /// <summary>Starts the stub CWP server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        cwp = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub CWP server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await cwp.DisposeAsync();
    }

    /// <summary>
    /// Creates a <see cref="CwpApiClient"/> aimed at the stub server, which answers the given JSON body over real HTTP.
    /// </summary>
    /// <param name="responseBody">JSON string to return as the HTTP response body.</param>
    /// <param name="statusCode">HTTP status code to return. Default 200 OK.</param>
    /// <returns>The configured <see cref="CwpApiClient"/>.</returns>
    private CwpApiClient BuildClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        cwp.Enqueue(statusCode, responseBody);
        return new CwpApiClient(new HttpClient(), NullLogger<CwpApiClient>.Instance);
    }

    /// <summary>CreateAccountAsync should return IsSuccess=true when CWP responds with status "OK".</summary>
    [Fact]
    public async Task CreateAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""");

        var result = await client.CreateAccountAsync(
            Connection, NewAccount, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Account created.");
    }

    /// <summary>CreateAccountAsync should return IsSuccess=false when CWP responds with status "Error".</summary>
    [Fact]
    public async Task CreateAccount_ReturnsFalse_WhenCwpRespondsErrorAsync()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "User already exists." }""");

        var result = await client.CreateAccountAsync(
            Connection, NewAccount, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User already exists.");
    }

    /// <summary>SuspendAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task SuspendAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account suspended." }""");

        var result = await client.SuspendAccountAsync(Connection, "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>UnsuspendAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task UnsuspendAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account unsuspended." }""");

        var result = await client.UnsuspendAccountAsync(Connection, "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>TerminateAccountAsync should return IsSuccess=true when CWP responds OK.</summary>
    [Fact]
    public async Task TerminateAccount_ReturnsTrue_WhenCwpRespondsOkAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account deleted." }""");

        var result = await client.TerminateAccountAsync(Connection, "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>An HTML page behind HTTP 200 is a failed call, not a JSON crash.</summary>
    [Fact]
    public async Task CreateAccount_Fails_WhenCwpAnswersWithAnHtmlPageAsync()
    {
        cwp.Enqueue(HttpStatusCode.OK, "<html><body>Login</body></html>", "text/html");
        var client = new CwpApiClient(new HttpClient(), NullLogger<CwpApiClient>.Instance);

        var result = await client.CreateAccountAsync(Connection, NewAccount, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("Login");
    }

    /// <summary>The account list is read for server info even when CWP answers with a page, as a failure to report.</summary>
    [Fact]
    public async Task GetServerInfo_Throws_WhenCwpAnswersWithAnHtmlPageAsync()
    {
        cwp.Enqueue(HttpStatusCode.OK, "<html><body>Login</body></html>", "text/html");
        var client = new CwpApiClient(new HttpClient(new RedirectToStubHandler(cwp.BaseAddress)), NullLogger<CwpApiClient>.Instance);

        var act = () => client.GetServerInfoAsync(new Innovayse.SDK.Plugins.HostingPanel.HostingPanelConnection("cwp.test", 2304, "test-key"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Server info counts the accounts and reads the version from two calls.</summary>
    [Fact]
    public async Task GetServerInfo_ReturnsTheCountAndVersionAsync()
    {
        cwp.Enqueue("""{ "status": "OK", "msj": [ { "user": "a", "domain": "a.test" }, { "user": "b", "domain": "b.test" } ] }""");
        cwp.Enqueue("""{ "status": "OK", "version": "0.9.8.1" }""");
        var client = new CwpApiClient(new HttpClient(new RedirectToStubHandler(cwp.BaseAddress)), NullLogger<CwpApiClient>.Instance);

        var info = await client.GetServerInfoAsync(new Innovayse.SDK.Plugins.HostingPanel.HostingPanelConnection("cwp.test", 2304, "test-key"), CancellationToken.None);

        info.AccountsCount.Should().Be(2);
        info.Version.Should().Be("0.9.8.1");
    }

    /// <summary>FindAccount returns the listed account with its domain and suspension flag.</summary>
    [Fact]
    public async Task FindAccount_ReturnsTheListedAccountAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": [ { "user": "testuser", "domain": "example.com", "suspended": "1" } ] }""");

        var account = await client.FindAccountAsync(Connection, "testuser", CancellationToken.None);

        account.Should().NotBeNull();
        account!.Domain.Should().Be("example.com");
        account.IsSuspended.Should().BeTrue();
    }

    /// <summary>FindAccount returns null when the listing succeeds without the account.</summary>
    [Fact]
    public async Task FindAccount_ReturnsNull_WhenTheAccountIsNotListedAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": [] }""");

        var account = await client.FindAccountAsync(Connection, "testuser", CancellationToken.None);

        account.Should().BeNull();
    }

    /// <summary>A failed listing is an error, never "no such account".</summary>
    [Fact]
    public async Task FindAccount_Throws_WhenTheListingFailsAsync()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "denied" }""");

        var act = () => client.FindAccountAsync(Connection, "testuser", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>SendAsync should throw HttpRequestException when the server returns 500.</summary>
    [Fact]
    public async Task CreateAccount_Throws_WhenServerReturns500Async()
    {
        var client = BuildClient("Internal Server Error", HttpStatusCode.InternalServerError);

        var act = () => client.CreateAccountAsync(
            Connection, NewAccount, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
