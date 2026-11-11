using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>UpdateTldConfig</c> use case:
/// <c>PUT /api/admin/tld-configs/{id}</c>. The happy path (create then update) lives in
/// <see cref="CreateTldConfigTests"/>; this file covers the id refusal.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateTldConfigTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating an id that names no configuration answers 404 with <c>TLD_CONFIG_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_Returns404WithTldConfigNotFoundAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/tld-configs/{DomainsTestSeeds.UnknownId}", new
        {
            registrarModule = "NameAm",
            currency = "AMD",
            sellCurrency = "USD",
            isEnabled = true,
            sortOrder = 0,
            categories = Array.Empty<string>(),
            costRegister = new Dictionary<string, decimal>(),
            costTransfer = new Dictionary<string, decimal>(),
            costRenew = new Dictionary<string, decimal>(),
            sellRegister = new Dictionary<string, decimal>(),
            sellTransfer = new Dictionary<string, decimal>(),
            sellRenew = new Dictionary<string, decimal>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/admin/tld-configs/{DomainsTestSeeds.UnknownId}", new
        {
            registrarModule = "NameAm",
            currency = "AMD",
            sellCurrency = "USD",
            isEnabled = true,
            sortOrder = 0,
            categories = Array.Empty<string>(),
            costRegister = new Dictionary<string, decimal>(),
            costTransfer = new Dictionary<string, decimal>(),
            costRenew = new Dictionary<string, decimal>(),
            sellRegister = new Dictionary<string, decimal>(),
            sellTransfer = new Dictionary<string, decimal>(),
            sellRenew = new Dictionary<string, decimal>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A negative sort order is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_NegativeSortOrder_Returns400ValidationErrorAsync()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/admin/tld-configs/{DomainsTestSeeds.UnknownId}", new
        {
            registrarModule = "NameAm",
            currency = "AMD",
            sellCurrency = "USD",
            isEnabled = true,
            sortOrder = -1,
            categories = Array.Empty<string>(),
            costRegister = new Dictionary<string, decimal>(),
            costTransfer = new Dictionary<string, decimal>(),
            costRenew = new Dictionary<string, decimal>(),
            sellRegister = new Dictionary<string, decimal>(),
            sellTransfer = new Dictionary<string, decimal>(),
            sellRenew = new Dictionary<string, decimal>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
