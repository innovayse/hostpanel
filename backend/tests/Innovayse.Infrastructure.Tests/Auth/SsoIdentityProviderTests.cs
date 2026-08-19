namespace Innovayse.Infrastructure.Tests.Auth;

using System.Net;
using System.Text;
using FluentAssertions;
using Innovayse.Infrastructure.Auth;
using Xunit;

/// <summary>
/// Unit tests for the SSO-backed identity provider, against a stubbed HTTP handler.
///
/// <para>
/// No database and no container: what is worth checking here is how this product reads
/// the SSO's replies, and above all that it tells "no such account" apart from "the SSO
/// did not answer". Confusing those two would let a network fault read as a deleted
/// customer.
/// </para>
/// </summary>
public sealed class SsoIdentityProviderTests
{
    /// <summary>Answers each request from a canned map of path prefix to response.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (SsoIdentityProvider Provider, StubHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sso.example.com/") };
        return (new SsoIdentityProvider(new SsoServiceClient(http)), handler);
    }

    [Fact]
    public async Task FindBySubjectAsync_ReturnsTheAccountAsync()
    {
        var (provider, _) = Build(_ => Json(HttpStatusCode.OK,
            """{"email":"ada@example.com","name":"Ada Lovelace","firstName":"Ada","lastName":"Lovelace"}"""));

        var found = await provider.FindBySubjectAsync("sub-1", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Subject.Should().Be("sub-1");
        found.Email.Should().Be("ada@example.com");
        found.FirstName.Should().Be("Ada");
        found.LastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task FindBySubjectAsync_WhenTheSsoHasNoSuchAccount_ReturnsNullAsync()
    {
        var (provider, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var found = await provider.FindBySubjectAsync("sub-unknown", CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindBySubjectAsync_WhenTheSsoFails_ThrowsRatherThanReturningNullAsync()
    {
        // The case this class exists for. A 500 means the answer is unknown; returning
        // null would tell the caller the person does not exist, and the admin screens
        // would show a live customer as an unresolved row.
        var (provider, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => provider.FindBySubjectAsync("sub-1", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FindBySubjectAsync_WhenTheServiceKeyIsRejected_ThrowsAsync()
    {
        // A misconfigured key is a deployment fault. Silently reporting every account as
        // missing is how that fault would look like data loss instead.
        var (provider, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => provider.FindBySubjectAsync("sub-1", CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FindByEmailAsync_ResolvesThroughTheLookupThenTheAccountAsync()
    {
        var (provider, handler) = Build(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/lookup")
                ? Json(HttpStatusCode.OK, """{"userId":"sub-42"}""")
                : Json(HttpStatusCode.OK,
                    """{"email":"grace@example.com","name":"Grace Hopper","firstName":"Grace","lastName":"Hopper"}"""));

        var found = await provider.FindByEmailAsync("grace@example.com", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Subject.Should().Be("sub-42");
        found.Email.Should().Be("grace@example.com");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindByEmailAsync_ForAnUnknownAddress_ReturnsNullWithoutASecondCallAsync()
    {
        var (provider, handler) = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var found = await provider.FindByEmailAsync("nobody@example.com", CancellationToken.None);

        found.Should().BeNull();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task FindByEmailAsync_EscapesTheAddressAsync()
    {
        // Addresses contain characters a query string treats as structure. Unescaped,
        // "a+b@example.com" reaches the SSO as "a b@example.com" and resolves to nobody.
        var (provider, handler) = Build(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/lookup")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        await provider.FindByEmailAsync("a+b@example.com", CancellationToken.None);

        handler.Requests[0].RequestUri!.Query.Should().Contain("a%2Bb%40example.com");
    }

    /// <summary>The batch reply, used by both bulk lookups below.</summary>
    private const string TwoAccounts =
        """
        {"users":[
          {"id":"sub-1","email":"ada@example.com","firstName":"Ada","lastName":"Lovelace","twoFactorEnabled":true},
          {"id":"sub-2","email":"grace@example.com","firstName":"Grace","lastName":"Hopper","twoFactorEnabled":false}]}
        """;

    [Fact]
    public async Task GetEmailsBySubjectsAsync_ReturnsWhatTheSsoKnowsAsync()
    {
        var (provider, _) = Build(_ => Json(HttpStatusCode.OK, TwoAccounts));

        var emails = await provider.GetEmailsBySubjectsAsync(
            ["sub-1", "sub-2", "sub-3"], CancellationToken.None);

        emails.Should().HaveCount(2);
        emails["sub-1"].Should().Be("ada@example.com");
        emails.ContainsKey("sub-3").Should().BeFalse();
    }

    [Fact]
    public async Task GetAccountsBySubjectsAsync_ReturnsWholeAccountsInOneCallAsync()
    {
        var (provider, handler) = Build(_ => Json(HttpStatusCode.OK, TwoAccounts));

        var accounts = await provider.GetAccountsBySubjectsAsync(
            ["sub-1", "sub-2", "sub-3"], CancellationToken.None);

        accounts.Should().HaveCount(2);
        accounts["sub-1"].Email.Should().Be("ada@example.com");
        accounts["sub-1"].FirstName.Should().Be("Ada");
        accounts["sub-1"].TwoFactorEnabled.Should().BeTrue();
        accounts["sub-2"].TwoFactorEnabled.Should().BeFalse();
        accounts.ContainsKey("sub-3").Should().BeFalse();

        // One request for the set, not one per subject — the whole reason this exists.
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAccountsBySubjectsAsync_WhenTheSsoFails_ThrowsAsync()
    {
        // Same rule as the single lookup: an empty page would read as "none of these
        // people exist", and every row on the screen would show as an orphan.
        var (provider, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var act = () => provider.GetAccountsBySubjectsAsync(["sub-1"], CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetEmailsBySubjectsAsync_ForNoSubjects_MakesNoRequestAsync()
    {
        var (provider, handler) = Build(_ => throw new InvalidOperationException(
            "the provider should not have called the SSO"));

        var emails = await provider.GetEmailsBySubjectsAsync([], CancellationToken.None);

        emails.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_ReturnsThePageAndTheUnpagedTotalAsync()
    {
        var (provider, handler) = Build(_ => Json(HttpStatusCode.OK,
            """
            {"total":57,"page":2,"pageSize":2,"users":[
              {"id":"sub-1","email":"ada@example.com","firstName":"Ada","lastName":"Lovelace"},
              {"id":"sub-2","email":"grace@example.com","firstName":"Grace","lastName":"Hopper"}]}
            """));

        var (items, total) = await provider.ListAsync("lace", page: 2, pageSize: 2, CancellationToken.None);

        total.Should().Be(57);
        items.Should().HaveCount(2);
        items[0].Subject.Should().Be("sub-1");

        var query = handler.Requests.Single().RequestUri!.Query;
        query.Should().Contain("page=2").And.Contain("pageSize=2").And.Contain("search=lace");
    }

    [Fact]
    public async Task ListAsync_WithoutASearch_SendsNoSearchParameterAsync()
    {
        var (provider, handler) = Build(_ => Json(HttpStatusCode.OK,
            """{"total":0,"page":1,"pageSize":25,"users":[]}"""));

        await provider.ListAsync(null, page: 1, pageSize: 25, CancellationToken.None);

        handler.Requests.Single().RequestUri!.Query.Should().NotContain("search=");
    }

    [Fact]
    public async Task ListAsync_WhenTheSsoFails_ThrowsRatherThanReturningAnEmptyPageAsync()
    {
        // An empty page reads as "this deployment has no users", which is a far more
        // alarming and far less actionable thing to show than an error.
        var (provider, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var act = () => provider.ListAsync(null, 1, 25, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
