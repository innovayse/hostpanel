using System.Net;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Providers.CWP7;
using Innovayse.Providers.CWP7.Models;
using Innovayse.SDK.Plugins.HostingPanel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Cwp7;

/// <summary>
/// Unit tests for <see cref="Cwp7ApiClient"/>'s response parsing.
/// </summary>
/// <remarks>
/// CWP7 answers errors with HTTP 200 and an HTML body, so <c>EnsureSuccessStatusCode</c> passes.
/// The client must read such a page as a failure, never as a provisioned account.
/// </remarks>
public sealed class Cwp7ApiClientTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the CWP7 host; started per test class instance.</summary>
    private StubHttpServer cwp7 = null!;

    /// <summary>Gets the stub server address without the trailing slash, as the client expects a base URL.</summary>
    private string BaseUrl => cwp7.BaseAddress.ToString().TrimEnd('/');

    /// <summary>Gets the connection to the stub server every test calls through.</summary>
    private Cwp7Connection Connection => new(BaseUrl, "test-key", "https://cwp.test:2083");

    /// <summary>Starts the stub CWP7 server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        cwp7 = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub CWP7 server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await cwp7.DisposeAsync();
    }

    /// <summary>The account every create-account test asks for.</summary>
    private static readonly Cwp7NewAccount NewAccount = new(
        "example.com", "testuser", "pass123", "default", "test@example.com", 0, 40, 150, "203.0.113.10");

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
    /// Builds a client aimed at the stub server, which answers the given body with HTTP 200 over real HTTP.
    /// </summary>
    /// <param name="body">The response body CWP7 answers with.</param>
    /// <param name="mediaType">The Content-Type to answer with.</param>
    /// <returns>The configured client.</returns>
    private Cwp7ApiClient BuildClient(string body, string mediaType)
    {
        cwp7.Enqueue(HttpStatusCode.OK, body, mediaType);
        return new Cwp7ApiClient(new HttpClient(), NullLogger<Cwp7ApiClient>.Instance);
    }

    /// <summary>Create account, fails, when cwp7 answers with an html error page.</summary>
    [Fact]
    public async Task CreateAccount_Fails_WhenCwp7AnswersWithAnHtmlErrorPageAsync()
    {
        var client = BuildClient(ErrorPageHtml, "text/html");

        var result = await client.CreateAccountAsync(Connection, NewAccount, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Creating an account keeps the page out of the result when the panel answers with HTML.</summary>
    [Fact]
    public async Task CreateAccount_KeepsThePageOutOfTheResult_WhenCwp7AnswersWithHtmlAsync()
    {
        var client = BuildClient(ErrorPageHtml, "text/html");

        var result = await client.CreateAccountAsync(Connection, NewAccount, CancellationToken.None);

        result.Message.Should().NotContain("Internal Server Error")
            .And.NotContain("not authorized")
            .And.NotContain("<html");
    }

    /// <summary>Suspending an account fails when the panel answers with an HTML error page.</summary>
    [Fact]
    public async Task SuspendAccount_Fails_WhenCwp7AnswersWithAnHtmlErrorPageAsync()
    {
        var client = BuildClient(ErrorPageHtml, "text/html");

        var result = await client.SuspendAccountAsync(
            Connection, "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    /// <summary>Creating an account succeeds when the panel answers with the JSON envelope.</summary>
    [Fact]
    public async Task CreateAccount_Succeeds_WhenCwp7AnswersWithTheJsonEnvelopeAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""", "application/json");

        var result = await client.CreateAccountAsync(Connection, NewAccount, CancellationToken.None);

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

    /// <summary>Auto login returns the session URL when the panel issues one.</summary>
    [Fact]
    public async Task AutoLogin_ReturnsTheSessionUrl_WhenCwp7IssuesOneAsync()
    {
        var client = BuildClient(UserSessionJson, "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            Connection, "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(
            "https://host3.example.com:2083/rootsage/?user_session=eac44313.f2acd74d.3d04bdcb.1788432569");
    }

    /// <summary>Auto login fails when the session is for a different user.</summary>
    [Fact]
    public async Task AutoLogin_Fails_WhenTheSessionIsForADifferentUserAsync()
    {
        // A session on somebody else's panel is worse than no session: the button would have
        // opened it and signed this client in as them.
        var client = BuildClient(UserSessionJson, "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            Connection, "someoneelse", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("eac44313");
    }

    /// <summary>Auto login fails when the panel refuses with a string message.</summary>
    [Fact]
    public async Task AutoLogin_Fails_WhenCwp7RefusesWithAStringMessageAsync()
    {
        // The refusal shape: `msj` is a string here, where a success carries an object. Reading
        // one into the other throws, and that must read as "no session", not as a crash.
        var client = BuildClient(
            """{"status":"Error","msj":"Unauthorized action abc123","format":"JSON"}""",
            "application/json");

        var result = await client.GetAutoLoginUrlAsync(
            Connection, "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().NotContain("Unauthorized").And.NotContain("abc123");
    }

    /// <summary>Auto login fails when the endpoint is not served.</summary>
    [Fact]
    public async Task AutoLogin_Fails_WhenTheEndpointIsNotServedAsync()
    {
        // A route CWP7 does not serve answers its 404 page with HTTP 200.
        var client = BuildClient(
            "<html><head><title>404 Page Not Found</title></head><body></body></html>",
            "text/html");

        var result = await client.GetAutoLoginUrlAsync(
            Connection, "rootsage", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    /// <summary>Auto login calls the user session route.</summary>
    [Fact]
    public async Task AutoLogin_CallsTheUserSessionRouteAsync()
    {
        // The session is only issued on this route, so the address is what this pins down.
        var client = BuildClient(UserSessionJson, "application/json");

        await client.GetAutoLoginUrlAsync(
            Connection, "rootsage", CancellationToken.None);

        cwp7.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/v1/user_session");
    }

    /// <summary>Creating an account sends the limits as culture-invariant numbers.</summary>
    [Fact]
    public async Task CreateAccount_SendsTheLimits_AsInvariantNumbersAsync()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""", "application/json");

        await client.CreateAccountAsync(Connection, NewAccount, CancellationToken.None);

        var body = cwp7.Requests.Should().ContainSingle().Which.Body;
        body.Should().Contain("inode=0").And.Contain("limit_nproc=40").And.Contain("limit_nofile=150");
    }

    /// <summary>Provisioning hides the panel's own text when the panel refuses.</summary>
    [Fact]
    public async Task Provision_HidesThePanelText_WhenCwp7RefusesAsync()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "Unauthorized action for key test-key" }""", "application/json");
        var provider = new Cwp7ProvisioningProvider(
            Connection, "203.0.113.10", client, NullLogger<Cwp7ProvisioningProvider>.Instance);

        var result = await provider.ProvisionAsync(
            new ProvisionAccountOrder(1, "example.com", "testuser", "pass123", "default"), CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        result.ErrorMessage.Should().NotContain("Unauthorized").And.NotContain("test-key");
    }

    /// <summary>Suspending hides the panel's own text when the panel refuses.</summary>
    [Fact]
    public async Task Suspend_HidesThePanelText_WhenCwp7RefusesAsync()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "Unauthorized action for key test-key" }""", "application/json");
        var provider = new Cwp7ProvisioningProvider(
            Connection, "203.0.113.10", client, NullLogger<Cwp7ProvisioningProvider>.Instance);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(1, "testuser", "overdue"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain("Unauthorized");
    }

    /// <summary>
    /// A packages-list refusal ("Error" with a text <c>msj</c>) surfaces the panel's own reason
    /// instead of a JSON conversion error, so a wrong API key reads as one in the server log.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetServerInfo_Refusal_ThrowsWithThePanelsReasonAsync()
    {
        var client = new Cwp7ApiClient(
            new HttpClient(new FixedReplyHttpMessageHandler("""{"status":"Error","msj":"key does not exist"}""")),
            NullLogger<Cwp7ApiClient>.Instance);

        var act = () => client.GetServerInfoAsync(
            new HostingPanelConnection("panel.example.test", 2304, "wrong-key"), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*key does not exist*");
    }

    /// <summary>A packages list answers with its entry count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task GetServerInfo_PackageList_ReturnsTheCountAsync()
    {
        var client = new Cwp7ApiClient(
            new HttpClient(new FixedReplyHttpMessageHandler("""{"status":"OK","msj":[{"id":1},{"id":2}]}""")),
            NullLogger<Cwp7ApiClient>.Instance);

        var info = await client.GetServerInfoAsync(
            new HostingPanelConnection("panel.example.test", 2304, "key"), CancellationToken.None);

        info.PackagesCount.Should().Be(2);
    }
}
