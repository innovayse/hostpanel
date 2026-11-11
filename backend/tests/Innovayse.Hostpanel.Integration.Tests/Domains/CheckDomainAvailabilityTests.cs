using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Domains.Common.Enums;
using Innovayse.Hostpanel.Application.Domains.Queries.CheckDomainAvailability;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>CheckDomainAvailability</c> use case: the anonymous
/// <c>GET</c>/<c>POST /api/domains/check</c> pair that lets the storefront check a domain name
/// before sign-in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckDomainAvailabilityTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// <c>POST /api/domains/check</c> with <c>{ "domain": "example.com" }</c>, the exact shape the
    /// client portal's own BFF route (<c>domain-check.post.ts</c>) sends. The action binds
    /// <see cref="CheckDomainAvailabilityQuery"/> directly, whose <c>Domain</c> property
    /// is the camelCase <c>domain</c> on the wire, so this exact body binds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailabilityPost_WithDomainField_Returns200Async()
    {
        var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync("/api/domains/check", new { domain = "available.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("domain").GetString().Should().Be("available.com");
        body.GetProperty("available").GetBoolean().Should().BeTrue();
        body.GetProperty("status").GetString().Should().Be(nameof(DomainAvailabilityStatus.Available));
    }

    /// <summary>The POST form trims the name in the handler, so padded input answers the bare name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailabilityPost_WithPaddedDomain_AnswersTheTrimmedNameAsync()
    {
        var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync("/api/domains/check", new { domain = "  available.com  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("domain").GetString().Should().Be("available.com");
    }

    /// <summary>A blank domain name is refused by the validator on the bus: 400 <c>VALIDATION_ERROR</c> with a field message.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailabilityPost_WithBlankDomain_Returns400ValidationErrorAsync()
    {
        var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync("/api/domains/check", new { domain = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.ValidationError);
        body.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    /// <summary>A body with no domain at all is refused the same way, not with a null-reference 500.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailabilityPost_WithoutDomainField_Returns400ValidationErrorAsync()
    {
        var httpClient = factory.CreateClient();

        var response = await httpClient.PostAsJsonAsync("/api/domains/check", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// The refusal's sentence follows <c>X-Language</c>: the storefront's BFF forwards the reader's
    /// language, so a Russian reader gets Russian text from the validator resources, not a literal.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailabilityPost_WithBlankDomain_AnswersInTheRequestLanguageAsync()
    {
        var httpClient = factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await httpClient.PostAsJsonAsync("/api/domains/check", new { domain = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>
    /// Anonymous caller hits <c>GET /api/domains/check?name=available.com</c>.
    /// <see cref="StubRegistrarProvider"/> always answers available, so the result reads available.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailability_ReturnsTrueAsync()
    {
        var httpClient = factory.CreateClient();

        var response = await httpClient.GetAsync("/api/domains/check?name=available.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("domain").GetString().Should().Be("available.com");
        body.GetProperty("available").GetBoolean().Should().BeTrue();
        body.GetProperty("status").GetString().Should().Be(nameof(DomainAvailabilityStatus.Available));
    }

    /// <summary>The GET form is reachable without a token too: the storefront needs it before sign-in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckAvailability_WithAdminToken_StillReturns200Async()
    {
        var httpClient = factory.CreateClient();
        var adminToken = await factory.GetAdminTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await httpClient.GetAsync("/api/domains/check?name=available.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
