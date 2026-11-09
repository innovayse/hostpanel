using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>TransferDomain</c> use case:
/// <c>POST /api/domains/transfer</c>, initiating an incoming domain transfer for a client. The
/// client-facing equivalent is <c>TransferMyDomain</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TransferDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A valid transfer request succeeds with 201 and a Location header pointing to the new
    /// domain: <see cref="StubRegistrarProvider.TransferAsync"/> always answers success.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_ValidRequest_Returns201Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "transfer-domain");

        var response = await admin.PostAsJsonAsync("/api/domains/transfer", new
        {
            clientId,
            domainName = $"{Guid.NewGuid():N}.test",
            eppCode = "EPP-SECRET-CODE",
            whoisPrivacy = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    /// <summary>
    /// A confirmed transfer records the period bought with it and a due date that follows the
    /// expiry, instead of the minimum date.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_Confirmed_RecordsPeriodAndDueDateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result<int>>(
            new TransferDomainCommand(clientId, $"{Guid.NewGuid():N}.test", "EPP-SECRET-CODE", WhoisPrivacy: false, Years: 3),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == result.Value, CancellationToken.None);
        reloaded.RegistrationPeriod.Should().Be(3);
        reloaded.NextDueDate.Should().Be(reloaded.ExpiresAt);
    }

    /// <summary>
    /// A transfer fulfilled from a paid order keeps the order's currency, so its recorded prices
    /// are not read as base-currency amounts.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_WithPriceCurrency_StoresThatCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result<int>>(
            new TransferDomainCommand(
                clientId, $"{Guid.NewGuid():N}.test", "EPP-SECRET-CODE", WhoisPrivacy: false,
                FirstPaymentAmount: 4600m, RecurringAmount: 4600m, PriceCurrency: "AMD"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var domain = await context.Domains.AsNoTracking().SingleAsync(d => d.Id == result.Value, CancellationToken.None);
        domain.PriceCurrency.Should().Be("AMD");
        domain.RecurringAmount.Should().Be(4600m);
    }

    /// <summary>A staff transfer that names no currency bills in the client's billing currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_WithoutPriceCurrency_StoresClientBillingCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var expected = await scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>()
            .ForClientAsync(clientId, CancellationToken.None);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result<int>>(
            new TransferDomainCommand(clientId, $"{Guid.NewGuid():N}.test", "EPP-SECRET-CODE", WhoisPrivacy: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var domain = await context.Domains.AsNoTracking().SingleAsync(d => d.Id == result.Value, CancellationToken.None);
        domain.PriceCurrency.Should().Be(expected.Code);
    }

    /// <summary>An EPP code shorter than the validator's minimum length refuses with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_WithShortEppCode_Returns400Async()
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (_, clientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "transfer-domain-short-epp");

        var response = await admin.PostAsJsonAsync("/api/domains/transfer", new
        {
            clientId,
            domainName = $"{Guid.NewGuid():N}.test",
            eppCode = "ab",
            whoisPrivacy = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Transfer_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/domains/transfer", new
        {
            clientId = 1,
            domainName = $"{Guid.NewGuid():N}.test",
            eppCode = "EPP-SECRET-CODE",
            whoisPrivacy = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
