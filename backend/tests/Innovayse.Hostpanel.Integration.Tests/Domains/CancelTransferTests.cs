using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>CancelTransfer</c> use case:
/// <c>POST /api/domains/{id}/cancel-transfer</c>, cancelling a pending incoming domain transfer.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Cancelling a pending transfer succeeds and cancels the domain aggregate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelTransfer_PendingTransfer_Returns204Async()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.CreateTransfer(clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsync($"/api/domains/{domain.Id}/cancel-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A registrar that offers no programmatic cancellation answers with the typed unsupported
    /// result; the use case maps it to 409 <c>REGISTRAR_OPERATION_UNSUPPORTED</c> and leaves the
    /// domain's status alone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelTransfer_RegistrarWithoutCancellation_Returns409AndKeepsTheTransferAsync()
    {
        var registrar = new RecordingRegistrar
        {
            CancelTransferAnswer = new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED),
        };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IRegistrarProvider>(registrar)));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.CreateTransfer(clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var response = await admin.PostAsync($"/api/domains/{domain.Id}/cancel-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.Status.Should().Be(DomainStatus.PendingTransfer);
    }

    /// <summary>An unknown domain id answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelTransfer_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/cancel-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelTransfer_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/cancel-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive domain id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelTransfer_NonPositiveDomainId_Returns400ValidationErrorAsync()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/domains/0/cancel-transfer", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
