using System.Net;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models.Enums;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Tests for how <see cref="WhmApiClient"/> reads WHM API 1 replies: success, refusal in an HTTP
/// 200 body, error statuses and bodies that are not WHM replies, against a stub server.
/// </summary>
public sealed class WhmApiClientTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for WHM.</summary>
    private StubHttpServer whm = null!;

    /// <summary>Starts the stub WHM server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        whm = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub WHM server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await whm.DisposeAsync();
    }

    /// <summary>The connection test reads the version.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetVersion_ReadsTheVersionAsync()
    {
        whm.Enqueue(WhmReplies.Version);

        var info = await Client().GetVersionAsync(Connection(), CancellationToken.None);

        info.Version.Should().Be("11.126.0.5");
        whm.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/json-api/version?api.version=1");
    }

    /// <summary>A refused or unreadable version call throws, so the connection test fails.</summary>
    /// <param name="status">The HTTP status WHM answers with.</param>
    /// <param name="body">The body WHM answers with.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.OK, WhmReplies.Refused)]
    [InlineData(HttpStatusCode.Forbidden, "Access denied")]
    [InlineData(HttpStatusCode.OK, WhmReplies.ErrorPage)]
    [InlineData(HttpStatusCode.OK, """{"metadata":{"result":"1"}}""")]
    public async Task GetVersion_RefusedOrUnreadable_ThrowsAsync(HttpStatusCode status, string body)
    {
        whm.Enqueue(status, body);

        var act = () => Client().GetVersionAsync(Connection(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>The connection test counts the accounts an unfiltered listaccts returns.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CountAccounts_CountsTheListAsync()
    {
        whm.Enqueue("""{"metadata":{"result":1},"data":{"acct":[{"user":"a"},{"user":"b"}]}}""");

        var count = await Client().CountAccountsAsync(Connection(), CancellationToken.None);

        count.Should().Be(2);
        whm.Requests.Should().ContainSingle().Which.PathAndQuery.Should().Be("/json-api/listaccts?api.version=1");
    }

    /// <summary>A refused account list throws, so the connection test fails instead of reporting zero.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CountAccounts_Refused_ThrowsAsync()
    {
        whm.Enqueue(WhmReplies.Refused);

        var act = () => Client().CountAccountsAsync(Connection(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>A lookup reads the account and its suspended flag, sent as a number or as text.</summary>
    /// <param name="flag">The suspended value as WHM sends it.</param>
    /// <param name="expected">Whether the account reads as suspended.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("\"1\"", true)]
    [InlineData("\"0\"", false)]
    public async Task FindAccount_ReadsTheSuspendedFlagAsync(string flag, bool expected)
    {
        whm.Enqueue($$$"""{"metadata":{"result":1},"data":{"acct":[{"user":"testuser","domain":"example.com","suspended":{{{flag}}},"plan":"Basic"}]}}""");

        var lookup = await Client().FindAccountAsync(Connection(), "testuser", CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.Found);
        lookup.Value.Should().Be(new WhmAccount("testuser", "example.com", expected, "Basic"));
    }

    /// <summary>A list that holds only another user is a positive "not found".</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FindAccount_OnlyAnotherUser_IsNotFoundAsync()
    {
        whm.Enqueue("""{"metadata":{"result":1},"data":{"acct":[{"user":"testuser2","domain":"other.example","suspended":0}]}}""");

        var lookup = await Client().FindAccountAsync(Connection(), "testuser", CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.NotFound);
    }

    /// <summary>An empty list, and a data object without the list, are a positive "not found".</summary>
    /// <param name="body">The reply body.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(WhmReplies.NoAccount)]
    [InlineData("""{"metadata":{"result":1},"data":{}}""")]
    public async Task FindAccount_EmptyAnswer_IsNotFoundAsync(string body)
    {
        whm.Enqueue(body);

        var lookup = await Client().FindAccountAsync(Connection(), "testuser", CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.NotFound);
    }

    /// <summary>A refusal, an error status, a non-WHM body or a missing data element is "unknown", never "not found".</summary>
    /// <param name="status">The HTTP status WHM answers with.</param>
    /// <param name="body">The body WHM answers with.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(HttpStatusCode.OK, WhmReplies.Refused)]
    [InlineData(HttpStatusCode.InternalServerError, WhmReplies.NoAccount)]
    [InlineData(HttpStatusCode.OK, WhmReplies.ErrorPage)]
    [InlineData(HttpStatusCode.OK, """{"metadata":{"result":1}}""")]
    [InlineData(HttpStatusCode.OK, """{"metadata":{"result":1},"data":{"acct":"none"}}""")]
    public async Task FindAccount_UnusableAnswer_IsUnknownAsync(HttpStatusCode status, string body)
    {
        whm.Enqueue(status, body);

        var lookup = await Client().FindAccountAsync(Connection(), "testuser", CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.Unknown);
    }

    /// <summary>A server that cannot be reached is an unreadable reply, not an exception.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_Unreachable_IsUnreadableAsync()
    {
        var address = whm.BaseAddress.ToString().TrimEnd('/');
        await whm.DisposeAsync();
        whm = await StubHttpServer.StartAsync();

        var reply = await Client().UnsuspendAccountAsync(new WhmConnection(address, "root", "t"), "testuser", CancellationToken.None);

        reply.Should().Be(WhmReply.Unreadable);
    }

    /// <summary>Creating an account sends the password in the form encoded body and never in the URL.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateAccount_SendsThePasswordInTheBodyNotTheUrlAsync()
    {
        whm.Enqueue(WhmReplies.Created);

        var reply = await Client().CreateAccountAsync(Connection(), "testuser", "example.com", "S3cret&pass word", "Basic", CancellationToken.None);

        reply.IsSuccess.Should().BeTrue();
        var call = whm.Requests.Should().ContainSingle().Subject;
        call.Method.Should().Be("POST");
        call.PathAndQuery.Should().Be("/json-api/createacct?api.version=1");
        call.Body.Should().Be("username=testuser&domain=example.com&password=S3cret%26pass+word&plan=Basic");
    }

    /// <summary>Changing a password sends it in the form encoded body and never in the URL.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePassword_SendsThePasswordInTheBodyNotTheUrlAsync()
    {
        whm.Enqueue(WhmReplies.Ok);

        var reply = await Client().ChangePasswordAsync(Connection(), "testuser", "N3w-p@ss", CancellationToken.None);

        reply.IsSuccess.Should().BeTrue();
        var call = whm.Requests.Should().ContainSingle().Subject;
        call.PathAndQuery.Should().Be("/json-api/passwd?api.version=1");
        call.Body.Should().Be("user=testuser&password=N3w-p%40ss");
    }

    /// <summary>A host name that is not a valid URI is an unreadable reply, logged by exception type only.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_InvalidHostName_IsUnreadableAndLogsOnlyTheTypeAsync()
    {
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));
        var client = new WhmApiClient(new HttpClient(), loggerFactory.CreateLogger<WhmApiClient>());

        var reply = await client.CreateAccountAsync(new WhmConnection("https://bad host:2087", "root", "secret-token-value"), "testuser", "example.com", "S3cret-pass", "Basic", CancellationToken.None);

        reply.Should().Be(WhmReply.Unreadable);
        var line = logs.Entries.Should().ContainSingle().Subject;
        line.Exception.Should().BeNull();
        line.Message.Should().Contain("UriFormatException").And.NotContain("bad host").And.NotContain("secret-token-value").And.NotContain("S3cret-pass");
    }

    /// <summary>No operation reaches WHM with an empty argument: a blank search would match every account.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_EmptyArgument_IsNeverSentAsync()
    {
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));
        var client = new WhmApiClient(new HttpClient(), loggerFactory.CreateLogger<WhmApiClient>());
        var connection = Connection();

        var lookup = await client.FindAccountAsync(connection, " ", CancellationToken.None);
        var removal = await client.RemoveAccountAsync(connection, string.Empty, CancellationToken.None);
        var creation = await client.CreateAccountAsync(connection, "testuser", string.Empty, "S3cret-pass", "Basic", CancellationToken.None);
        var password = await client.ChangePasswordAsync(connection, "testuser", string.Empty, CancellationToken.None);
        var session = await client.CreateUserSessionAsync(connection, string.Empty, CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.Unknown);
        removal.Should().Be(WhmReply.Unreadable);
        creation.Should().Be(WhmReply.Unreadable);
        password.Should().Be(WhmReply.Unreadable);
        session.Should().BeNull();
        whm.Requests.Should().BeEmpty();
        logs.Entries.Should().NotBeEmpty().And.OnlyContain(e => !e.Message.Contains("S3cret-pass"));
    }

    /// <summary>A suspend with a blank reason is sent without the reason, since WHM takes it as optional.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SuspendAccount_BlankReason_LeavesTheReasonOutAsync()
    {
        whm.Enqueue(WhmReplies.Ok);

        var reply = await Client().SuspendAccountAsync(Connection(), "testuser", string.Empty, CancellationToken.None);

        reply.IsSuccess.Should().BeTrue();
        whm.Requests.Should().ContainSingle().Which.Body.Should().Be("user=testuser");
    }

    /// <summary>A body larger than the cap is an unreadable reply, never held in memory whole.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_ResponseLargerThanTheCap_IsUnreadableAsync()
    {
        var filler = new string('a', (int)WhmApiClient.MaxResponseBytes);
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));
        var client = new WhmApiClient(new HttpClient(), loggerFactory.CreateLogger<WhmApiClient>());

        // Each call gets its own oversized reply, queued right before it, so the reply a call
        // asserts on is the one that call receives.
        whm.Enqueue(VersionReply(filler));
        var version = () => client.GetVersionAsync(Connection(), CancellationToken.None);
        await version.Should().ThrowAsync<InvalidOperationException>("an unreadable reply makes the version call throw");
        whm.Requests.Should().ContainSingle().Which.PathAndQuery.Should().StartWith("/json-api/version");

        whm.Enqueue(VersionReply(filler));
        var lookup = await client.FindAccountAsync(Connection(), "testuser", CancellationToken.None);

        lookup.Outcome.Should().Be(WhmLookupOutcome.Unknown, "a body past the cap never reads as a positive answer");
        whm.Requests.Should().HaveCount(2).And.Subject.Last().PathAndQuery.Should().StartWith("/json-api/listaccts");
        logs.Entries.Count(e => e.Message.Contains("could not be sent") && e.Message.Contains(nameof(HttpRequestException))).Should().Be(2);
    }

    /// <summary>A body just under the cap is still read.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_ResponseUnderTheCap_IsReadAsync()
    {
        var filler = new string('a', (int)WhmApiClient.MaxResponseBytes / 2);
        whm.Enqueue(VersionReply(filler));

        var info = await Client().GetVersionAsync(Connection(), CancellationToken.None);

        info.Version.Should().HaveLength(filler.Length);
    }

    /// <summary>A token cancelled before the call throws and sends nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_TokenAlreadyCancelled_ThrowsAndSendsNothingAsync()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => Client().CreateAccountAsync(Connection(), "testuser", "example.com", "S3cret-pass", "Basic", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        whm.Requests.Should().BeEmpty();
    }

    /// <summary>A token cancelled while WHM holds the request throws instead of reading as an unreadable reply.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_CancelledWhileWaiting_ThrowsRatherThanUnreadableAsync()
    {
        whm.Enqueue(new StubReply(HttpStatusCode.OK, WhmReplies.Ok, Delay: TimeSpan.FromSeconds(30)));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var act = () => Client().UnsuspendAccountAsync(Connection(), "testuser", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        whm.Requests.Should().ContainSingle();
    }

    /// <summary>A client that gives up on a held request by its own timeout, not the caller's token, reads as an unreadable reply.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_ClientTimeout_IsUnreadableAsync()
    {
        whm.Enqueue(new StubReply(HttpStatusCode.OK, WhmReplies.Ok, Delay: TimeSpan.FromSeconds(30)));
        var client = new WhmApiClient(new HttpClient { Timeout = TimeSpan.FromMilliseconds(300) }, NullLogger<WhmApiClient>.Instance);

        var reply = await client.UnsuspendAccountAsync(Connection(), "testuser", CancellationToken.None);

        reply.Should().Be(WhmReply.Unreadable);
    }

    /// <summary>
    /// The real registration's per-attempt timeout abandons a held request and reads as an
    /// unreadable reply, logged by exception type only.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_ResilienceTimeout_IsUnreadableAndLogsOnlyTheTypeAsync()
    {
        await using var held = await StubHttpServer.StartAsync();
        held.Enqueue(new StubReply(HttpStatusCode.OK, WhmReplies.Ok, Delay: TimeSpan.FromSeconds(30)));
        await using var host = WhmTestHost.Build(o =>
        {
            o.CPanelWhm.AttemptTimeout = TimeSpan.FromMilliseconds(300);
            o.CPanelWhm.TotalTimeout = TimeSpan.FromSeconds(2);
        });
        var started = DateTimeOffset.UtcNow;

        var reply = await host.Client().ChangePasswordAsync(WhmTestHost.ConnectionTo(held), "testuser", "S3cret-pass", CancellationToken.None);

        reply.Should().Be(WhmReply.Unreadable);
        (DateTimeOffset.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(10));
        held.Requests.Should().ContainSingle("a timed out call is not repeated");
        var line = host.Logs.Entries.Should().Contain(e => e.Message.Contains("could not be sent")).Subject;
        line.Message.Should().Contain("TimeoutRejectedException").And.NotContain("S3cret-pass").And.NotContain("api-token-value");
    }

    /// <summary>The connection prints without its API token.</summary>
    [Fact]
    public void Connection_ToString_HidesTheToken()
    {
        new WhmConnection("https://whm.example.test:2087", "root", "secret-token-value").ToString()
            .Should().NotContain("secret-token-value").And.Contain("root");
    }

    /// <summary>Builds a successful <c>version</c> reply whose version text is the given value.</summary>
    /// <param name="version">The version text.</param>
    /// <returns>The reply body.</returns>
    private static string VersionReply(string version) =>
        "{\"metadata\":{\"result\":1},\"data\":{\"version\":\"" + version + "\"}}";

    /// <summary>Builds the client under test.</summary>
    /// <returns>The client.</returns>
    private static WhmApiClient Client() => new(new HttpClient(), NullLogger<WhmApiClient>.Instance);

    /// <summary>The connection to the stub WHM server.</summary>
    /// <returns>The connection.</returns>
    private WhmConnection Connection() => new(whm.BaseAddress.ToString().TrimEnd('/'), "root", "token");
}
