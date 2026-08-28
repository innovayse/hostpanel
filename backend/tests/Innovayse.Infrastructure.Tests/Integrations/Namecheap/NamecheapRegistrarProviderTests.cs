namespace Innovayse.Infrastructure.Tests.Integrations.Namecheap;

using System.Net;
using System.Text;
using FluentAssertions;
using Innovayse.Domain.Domains;
using Innovayse.Infrastructure.Integrations.Namecheap;
using Innovayse.Infrastructure.Integrations.Namecheap.Options;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

/// <summary>
/// Unit tests for <see cref="NamecheapRegistrarProvider"/>'s previously-stubbed operations —
/// contact updates, email forwarding, and the DNS-management toggle — verifying both the
/// XML response parsing and the exact query parameters sent to the Namecheap API.
/// </summary>
public sealed class NamecheapRegistrarProviderTests
{
    private static NamecheapOptions Settings => new()
    {
        ApiUser = "testuser",
        ApiKey = "testkey",
        ClientIp = "127.0.0.1",
        ApiUrl = "https://api.sandbox.namecheap.com/xml.response",
    };

    /// <summary>
    /// Builds a provider wired to a mock HTTP handler that always returns <paramref name="responseXml"/>,
    /// and captures the last request URL sent so tests can assert on the query parameters.
    /// </summary>
    private static (NamecheapRegistrarProvider Provider, Func<string> LastRequestUrl) BuildProvider(string responseXml)
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
                Content = new StringContent(responseXml, Encoding.UTF8, "text/xml"),
            });

        var http = new HttpClient(mockHandler.Object);
        var client = new NamecheapClient(http, Options.Create(Settings));
        return (new NamecheapRegistrarProvider(client), () => lastUrl ?? string.Empty);
    }

    /// <summary>
    /// Extracts a single query parameter's decoded value from a request URL.
    /// Avoids depending on System.Web/AspNetCore query-parsing helpers just for tests.
    /// </summary>
    private static string QueryParam(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == name)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return string.Empty;
    }

    [Fact]
    public async Task ModifyContactDetailsAsync_SendsSetContactsCommand_ForAllFourRolesAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse><DomainSetContactResult Domain="example.com" IsSuccess="true" /></CommandResponse></ApiResponse>""";
        var (provider, lastUrl) = BuildProvider(response);

        var contact = new DomainContact(
            "Jane", "Doe", "Acme Inc", "jane@example.com", "+1.5551234567",
            "123 Main St", null, "Springfield", "IL", "62701", "US");

        var result = await provider.ModifyContactDetailsAsync("example.com", contact, CancellationToken.None);

        result.Success.Should().BeTrue();
        var url = lastUrl();
        QueryParam(url, "Command").Should().Be("namecheap.domains.setContacts");
        QueryParam(url, "RegistrantFirstName").Should().Be("Jane");
        QueryParam(url, "TechEmailAddress").Should().Be("jane@example.com");
        QueryParam(url, "AdminCountry").Should().Be("US");
        QueryParam(url, "AuxBillingLastName").Should().Be("Doe");
    }

    [Fact]
    public async Task SetEmailForwardingAsync_WhenDisabling_ClearsAllForwardsAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""";
        var (provider, lastUrl) = BuildProvider(response);

        var result = await provider.SetEmailForwardingAsync("example.com", enabled: false, CancellationToken.None);

        result.Success.Should().BeTrue();
        QueryParam(lastUrl(), "Command").Should().Be("namecheap.domains.dns.setEmailForwarding");
        QueryParam(lastUrl(), "MailBox1").Should().BeEmpty();
    }

    [Fact]
    public async Task AddEmailForwardingRuleAsync_MergesWithExistingForwardsAsync()
    {
        // First call (GetEmailForwardsAsync inside AddEmailForwardingRuleAsync) returns one
        // existing forward; the provider must resend it alongside the newly added rule.
        const string getResponse = """
            <?xml version="1.0"?>
            <ApiResponse Status="OK">
              <CommandResponse>
                <DomainDNSGetEmailForwardingResult>
                  <Forward mailbox="sales">sales@destination.com</Forward>
                </DomainDNSGetEmailForwardingResult>
              </CommandResponse>
            </ApiResponse>
            """;

        var mockHandler = new Mock<HttpMessageHandler>();
        var capturedUrls = new List<string>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrls.Add(req.RequestUri!.ToString()))
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(getResponse, Encoding.UTF8, "text/xml"),
            });

        var http = new HttpClient(mockHandler.Object);
        var provider = new NamecheapRegistrarProvider(new NamecheapClient(http, Options.Create(Settings)));

        var result = await provider.AddEmailForwardingRuleAsync(
            "example.com", "support", "support@destination.com", CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedUrls.Should().HaveCount(2);

        var setUrl = capturedUrls[1];
        QueryParam(setUrl, "Command").Should().Be("namecheap.domains.dns.setEmailForwarding");

        var mailboxes = new[] { QueryParam(setUrl, "MailBox1"), QueryParam(setUrl, "MailBox2") };
        mailboxes.Should().Contain("sales").And.Contain("support");
    }

    [Fact]
    public async Task SetDnsManagementAsync_WhenEnabling_CallsSetDefaultAsync()
    {
        const string response = """<?xml version="1.0"?><ApiResponse Status="OK"><CommandResponse /></ApiResponse>""";
        var (provider, lastUrl) = BuildProvider(response);

        var result = await provider.SetDnsManagementAsync("example.com", enabled: true, CancellationToken.None);

        result.Success.Should().BeTrue();
        QueryParam(lastUrl(), "Command").Should().Be("namecheap.domains.dns.setDefault");
    }

    [Fact]
    public async Task SetDnsManagementAsync_WhenDisabling_MakesNoApiCallAsync()
    {
        var callCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback(() => callCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ApiResponse Status=\"OK\"/>") });

        var http = new HttpClient(mockHandler.Object);
        var provider = new NamecheapRegistrarProvider(new NamecheapClient(http, Options.Create(Settings)));

        var result = await provider.SetDnsManagementAsync("example.com", enabled: false, CancellationToken.None);

        result.Success.Should().BeTrue();
        callCount.Should().Be(0);
    }
}
