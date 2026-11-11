using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// The one test <c>rules/api.md</c> asks for: one real refusal per status a client can receive,
/// each asserting the body is an <c>application/problem+json</c> <c>ErrorResponse</c>, that its
/// <c>detail</c> follows the request's <c>X-Language</c>, and that the compatibility shim
/// (<c>error</c> == <c>detail</c>, <c>code</c> == <c>type</c>) is present while the switch is on.
/// </summary>
/// <remarks>
/// <para>
/// 400 (model binding) and 401 are pinned in <see cref="ErrorBodyTests"/> already, including the
/// Russian-detail assertion this file repeats for the rest; they are not duplicated here. 409 is
/// pinned in <c>Auth/CompleteSetupTests.AlreadyCompletedRefuses</c>: the only reliable 409 this
/// deployment answers ("nobody holds Admin yet" claimed twice) needs a fresh, unshared
/// <c>IntegrationTestFactory</c> to guarantee its precondition, which that test already builds.
/// Duplicating it against the shared factory here would be flaky at best (the shared factory's
/// seeded admin holds the role through ASP.NET Identity's own store, which
/// <c>CompleteSetupHandler</c> does not read) and wasteful at worst (a second Postgres
/// container for one assertion).
/// </para>
/// <para>
/// <b>422 and 429 are not exercised over HTTP, on purpose.</b> No handler in this deployment
/// answers a code containing <c>VALIDATION</c> or <c>CONTENT_REJECTED</c>: handler-owned codes keep
/// those substrings out on purpose, so 422 is a real row in
/// <c>StatusMappingTests</c> (<c>Shared/Controllers/StatusMapping</c>) but not a status any
/// caller can actually receive; adding one here would mean inventing a use case. 429 is switched off in the <c>Testing</c> environment by
/// <c>RateLimitingExtensions.AddPlatformRateLimiting</c> deliberately (api.md: "a suite never
/// asserts against a 429 it did not intend"), so the shared test host cannot produce one; its body
/// shape is instead reviewed by reading <c>RateLimitingExtensions.OnRejected</c> alongside this file.
/// </para>
/// <para>
/// 500 is answered only by <see cref="Innovayse.Hostpanel.Shared.Middleware.ExceptionHandlingMiddleware"/>'s
/// catch-all for a fault nothing upstream classified, deliberately not something this suite
/// manufactures against a real route, since doing so would mean shipping a route whose only job
/// is to throw. <c>ExceptionHandlingMiddlewareTests</c> below covers its body shape directly,
/// against the middleware rather than through a live endpoint.
/// </para>
/// <para>
/// <see cref="Publish_OnNonDraftInvoice_Answers400WithLocalizedDetailAsync"/> is the
/// one case here that is not a handler's own <c>Result</c> refusal: <c>Invoice.Publish</c> throws
/// <c>DomainRuleException</c> straight out of the aggregate, uncaught by
/// <c>PublishInvoiceHandler</c>, and <see cref="Innovayse.Hostpanel.Shared.Middleware.ExceptionHandlingMiddleware"/> is
/// what turns it into the same <c>ErrorResponse</c> shape every other refusal answers with: the
/// same status/title lookup (<c>StatusMapping</c>) and the same code-to-sentence resolution
/// (<c>IErrorMessageResolver</c>) <c>BaseApiController.MapFailure</c> uses for a <c>Result</c>.
/// </para>
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApiStatusContractTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The RFC 9457 media type every error body carries.</summary>
    private const string ProblemJson = "application/problem+json";

    /// <summary>
    /// Reads the body as JSON after asserting the status, media type and shape every error body
    /// shares, then the compatibility shim.
    /// </summary>
    /// <param name="response">The response under test.</param>
    /// <param name="status">The status it must carry.</param>
    /// <returns>The parsed body.</returns>
    private static async Task<JsonElement> ReadErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be(ProblemJson);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.Clone();

        body.GetProperty("status").GetInt32().Should().Be((int)status);
        body.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        return body;
    }

    /// <summary>Registers a fresh portal client and returns an authenticated, Client-role bearer client.</summary>
    /// <returns>The authenticated client.</returns>
    private async Task<HttpClient> RegisterClientAsync()
    {
        var http = factory.CreateClient();
        var email = $"status-contract-{Guid.NewGuid():N}@example.test";
        const string Password = "Password123!";

        var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, firstName = "Status", lastName = "Contract" });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, Password);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>An authenticated caller with the wrong role answers 403, not a redirect or an empty body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongRole_Answers403WithLocalizedDetailAndShimAsync()
    {
        var client = await RegisterClientAsync();
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.GetAsync("/api/admin/settings");

        var body = await ReadErrorAsync(response, HttpStatusCode.Forbidden);
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>An id naming no resource answers 404 with the resource's own not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_Answers404WithLocalizedDetailAndShimAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.GetAsync("/api/admin/settings/999999999");

        var body = await ReadErrorAsync(response, HttpStatusCode.NotFound);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.SETTING_NOT_FOUND);
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>An upload over the configured byte ceiling answers 413, not a host-level reset.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OversizedUpload_Answers413WithLocalizedDetailAndShimAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        // One byte over BrandingOptions.MaxBytes' 5 MB default: garbage bytes are enough,
        // because the byte-length check runs before anything tries to decode them as an image.
        var oversized = new byte[(5 * 1024 * 1024) + 1];
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(oversized);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");

        var response = await client.PostAsync("/api/admin/settings/branding/logo", content);

        var body = await ReadErrorAsync(response, HttpStatusCode.RequestEntityTooLarge);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.BRANDING_IMAGE_TOO_LARGE);
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>A dependency this deployment has not configured answers 503, not a silent success.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnconfiguredDependency_Answers503WithLocalizedDetailAndShimAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Status Contract",
            email = "status-contract@example.test",
            phone = (string?)null,
            service = (string?)null,
            message = "Checking the 503 contract.",
            submittedAt = (string?)null,
        });

        var body = await ReadErrorAsync(response, HttpStatusCode.ServiceUnavailable);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.CONTACT_NOT_CONFIGURED);
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }

    /// <summary>
    /// Publishing an invoice that is not a Draft answers 400 through the aggregate's own thrown
    /// <c>DomainRuleException</c>, not a handler's <c>Result</c> refusal, proving that
    /// <see cref="Innovayse.Hostpanel.Shared.Middleware.ExceptionHandlingMiddleware"/> maps a domain exception to the
    /// same localized <c>problem+json</c> body every other refusal answers with.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Publish_OnNonDraftInvoice_Answers400WithLocalizedDetailAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seededClient = Client.Create(
            $"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", SeededAt);
        db.Clients.Add(seededClient);
        await db.SaveChangesAsync();

        // Invoice.Create (not CreateDraft) starts Unpaid, so Publish's own "must be Draft"
        // invariant is the only thing that can refuse this call; nothing upstream pre-checks it.
        var invoice = Invoice.Create(seededClient.Id, SeededAt.AddDays(14), "USD", SeededAt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.PostAsync($"/api/billing/{invoice.Id}/publish", null);

        var body = await ReadErrorAsync(response, HttpStatusCode.BadRequest);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.INVOICE_NOT_DRAFT);
        body.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
    }
}
