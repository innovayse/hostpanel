using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;

/// <summary>
/// Tests for <see cref="WhmProvisioningProvider"/> against a stub WHM server: the calls it sends,
/// its retry-safe answers (account already there, already suspended, already gone, already on the
/// package) and what it keeps out of its logs. The reply bodies follow the WHM API 1 reference,
/// not a live server.
/// </summary>
public sealed class WhmProvisioningProviderTests : IAsyncLifetime
{
    /// <summary>The API token the stub expects; it must never be logged.</summary>
    private const string Token = "WHMTOKEN0123456789";

    /// <summary>The order every provision test sends.</summary>
    private static readonly ProvisionAccountOrder Order = new(7, "example.com", "testuser", "p&ss=1 2", "Basic");

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

    /// <summary>A fresh order calls createacct with every argument escaped, by GET, with the WHM token header.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_CallsCreateacctAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Created);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Created);
        result.ProvisioningRef.Should().Be("testuser");
        var call = whm.Requests.Should().ContainSingle().Subject;
        AssertCall(call, "createacct", "username=testuser&domain=example.com&password=p%26ss%3D1+2&plan=Basic");
        call.Authorization.Should().Be($"whm root:{Token}");
    }

    /// <summary>A refused create whose user already exists reports it with its domain, and changes nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_ExistingAccount_ReportsItWithItsDomainAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.ActiveAccount);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
        result.ExistingAccountDomain.Should().Be("example.com");
        whm.Requests.Should().HaveCount(2);
        AssertCall(whm.Requests[1], "listaccts", "search=testuser&searchtype=user&searchmethod=exact");
    }

    /// <summary>A refused create with no such account is refused, without WHM's own text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_RefusedAndNoAccount_IsRefusedWithoutWhmTextAsync()
    {
        var logger = new CapturingLogger<WhmProvisioningProvider>();
        var provider = BuildProvider(logger, WhmReplies.Refused, WhmReplies.NoAccount);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
        result.ErrorMessage.Should().NotContain("taken");
        logger.Messages.Should().NotContain(m => m.Contains("taken") || m.Contains("example.com"));
    }

    /// <summary>A lost create answer with the account made reports it, so the retry adopts it after the domain check.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_LostAnswerButAccountMade_ReportsItAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.ErrorPage, WhmReplies.ActiveAccount);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.AlreadyExists);
    }

    /// <summary>A lost create answer whose lookup is also lost is refused, never reported as created.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Provision_LostAnswerAndLostLookup_IsRefusedAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.ErrorPage, WhmReplies.ErrorPage);

        var result = await provider.ProvisionAsync(Order, CancellationToken.None);

        result.Outcome.Should().Be(ProvisioningOutcome.Refused);
    }

    /// <summary>Suspend sends the user and the reason to suspendacct.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_CallsSuspendacctAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Ok);

        await provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue invoice"), CancellationToken.None);

        AssertCall(whm.Requests.Should().ContainSingle().Subject, "suspendacct", "user=testuser&reason=Overdue+invoice");
    }

    /// <summary>A refused suspend of an account already suspended succeeds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_AlreadySuspended_SucceedsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.SuspendedAccount);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A refused suspend of an active account throws.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspend_RefusedAndStillActive_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.ActiveAccount);

        var act = () => provider.SuspendAsync(new SuspendAccountOrder(7, "testuser", "Overdue"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Unsuspend calls unsuspendacct, and a refusal for an account already active succeeds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_AlreadyActive_SucceedsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.ActiveAccount);

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        AssertCall(whm.Requests[0], "unsuspendacct", "user=testuser");
    }

    /// <summary>A refused unsuspend whose lookup is lost throws, never assumes success.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspend_RefusedAndLookupLost_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.ErrorPage);

        var act = () => provider.UnsuspendAsync("testuser", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Terminate calls removeacct with <c>username</c>, the argument name the reference requires.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_CallsRemoveacctWithUsernameAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Ok);

        await provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "Cancelled"), CancellationToken.None);

        AssertCall(whm.Requests.Should().ContainSingle().Subject, "removeacct", "username=testuser");
    }

    /// <summary>A refused terminate of an account that is already gone succeeds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_AlreadyGone_SucceedsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, WhmReplies.NoAccount);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "Cancelled"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    /// <summary>A lost terminate answer whose lookup is also lost throws, so the account is never assumed gone.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Terminate_LostAnswerAndLostLookup_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.ErrorPage, WhmReplies.ErrorPage);

        var act = () => provider.TerminateAsync(new TerminateAccountOrder(7, "testuser", "Cancelled"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Change package sends the user and <c>pkg</c> to changepackage.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePackage_CallsChangepackageAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Ok);

        await provider.ChangePackageAsync("testuser", "Pro Plan", CancellationToken.None);

        AssertCall(whm.Requests.Should().ContainSingle().Subject, "changepackage", "user=testuser&pkg=Pro+Plan");
    }

    /// <summary>A refused change succeeds when the account is already on the package, and throws when it is not.</summary>
    /// <param name="lookup">The account as the lookup finds it.</param>
    /// <param name="succeeds">Whether the change is accepted.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(WhmReplies.ProAccount, true)]
    [InlineData(WhmReplies.ActiveAccount, false)]
    public async Task ChangePackage_Refused_SucceedsOnlyWhenAlreadyOnThePackageAsync(string lookup, bool succeeds)
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused, lookup);

        var act = () => provider.ChangePackageAsync("testuser", "Pro", CancellationToken.None);

        if (succeeds)
        {
            await act.Should().NotThrowAsync();
        }
        else
        {
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    /// <summary>A password change sends passwd, and a refusal throws.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangePassword_Refused_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused);

        var act = () => provider.ChangePasswordAsync("testuser", "N3w", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        AssertCall(whm.Requests.Should().ContainSingle().Subject, "passwd", "user=testuser&password=N3w");
    }

    /// <summary>Single sign-on returns the URL create_user_session gives for the cpaneld service.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Sso_ReturnsTheSessionUrlAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Session);

        var url = await provider.GetCPanelSsoUrlAsync("testuser", null, CancellationToken.None);

        url.Should().Be("https://whm.example.test:2083/cpsess123/login/?session=testuser:abc");
        AssertCall(whm.Requests.Should().ContainSingle().Subject, "create_user_session", "user=testuser&service=cpaneld");
    }

    /// <summary>A refused session throws.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Sso_Refused_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.Refused);

        var act = () => provider.GetCPanelSsoUrlAsync("testuser", null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Credentials carry the account's domain from WHM and the cPanel address, never a password.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCredentials_ReadsTheDomainFromWhmAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.ActiveAccount);

        var credentials = await provider.GetCredentialsAsync("testuser", CancellationToken.None);

        credentials.Domain.Should().Be("example.com");
        credentials.Password.Should().BeNull();
        credentials.CpanelUrl.Should().Be("https://whm.example.test:2083");
    }

    /// <summary>Credentials for an account WHM does not hold throw instead of making up a domain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetCredentials_NoAccount_ThrowsAsync()
    {
        var provider = BuildProvider(new CapturingLogger<WhmProvisioningProvider>(), WhmReplies.NoAccount);

        var act = () => provider.GetCredentialsAsync("testuser", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>No log line of the provider or the client holds the token, the password or WHM's text.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Logs_NeverHoldTheTokenThePasswordOrWhmTextAsync()
    {
        var logger = new CapturingLogger<WhmProvisioningProvider>();
        var clientLogger = new CapturingLogger<WhmApiClient>();
        foreach (var answer in new[] { WhmReplies.Refused, WhmReplies.ErrorPage })
        {
            whm.Enqueue(System.Net.HttpStatusCode.OK, answer);
        }

        var provider = new WhmProvisioningProvider(Connection(), "192.0.2.10", "https://whm.example.test:2083", new WhmApiClient(new HttpClient(), clientLogger), logger);

        await provider.ProvisionAsync(Order, CancellationToken.None);

        var lines = logger.Messages.Concat(clientLogger.Messages).ToList();
        lines.Should().NotBeEmpty();
        lines.Should().NotContain(m => m.Contains(Token) || m.Contains("p&ss") || m.Contains("p%26ss") || m.Contains("taken") || m.Contains("api.version"));
    }

    /// <summary>Builds a provider whose WHM answers the given bodies in order.</summary>
    /// <param name="logger">The logger the provider writes to.</param>
    /// <param name="answers">The bodies, one per expected call.</param>
    /// <returns>The provider under test.</returns>
    private WhmProvisioningProvider BuildProvider(CapturingLogger<WhmProvisioningProvider> logger, params string[] answers)
    {
        foreach (var answer in answers)
        {
            whm.Enqueue(System.Net.HttpStatusCode.OK, answer);
        }

        var client = new WhmApiClient(new HttpClient(), NullLogger<WhmApiClient>.Instance);
        return new WhmProvisioningProvider(Connection(), "192.0.2.10", "https://whm.example.test:2083", client, logger);
    }

    /// <summary>The connection to the stub WHM server.</summary>
    /// <returns>The connection.</returns>
    private WhmConnection Connection() => new(whm.BaseAddress.ToString().TrimEnd('/'), "root", Token);

    /// <summary>Asserts a call was a POST to the function with its arguments in the body and none in the URL.</summary>
    /// <param name="call">The recorded call.</param>
    /// <param name="function">The WHM API 1 function name.</param>
    /// <param name="body">The expected form encoded body.</param>
    private static void AssertCall(RecordedCall call, string function, string body)
    {
        call.Method.Should().Be("POST");
        call.PathAndQuery.Should().Be($"/json-api/{function}?api.version=1");
        call.Body.Should().Be(body);
    }
}
