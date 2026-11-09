using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetRenewableServices;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="GetRenewableServicesQuery"/> through the real <see cref="IMessageBus"/>: the active,
/// auto-renewing services due inside the window, each flagged with an open cancellation request.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRenewableServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A due service is listed with its billing data and its open cancellation request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_DueServiceWithOpenCancellation_IsListedFlaggedAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(5));
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.CancellationRequests.Add(CancellationRequest.Create(seeded.ServiceId, CancellationType.EndOfBillingPeriod, "Moving"));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var due = await InvokeAsync(host, new GetRenewableServicesQuery(seeded.ClientId, DateTimeOffset.UtcNow.AddDays(14)));

        var service = due.Should().ContainSingle().Subject;
        service.Id.Should().Be(seeded.ServiceId);
        service.ProductId.Should().Be(seeded.ProductId);
        service.RecurringAmount.Should().Be(5m);
        service.HasOpenCancellation.Should().BeTrue();
    }

    /// <summary>A service renewing after the window, or with no renewal date, is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ServiceOutsideWindowOrUndated_IsLeftOutAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var later = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(60));
        var undated = await RenewalTestSeeds.SeedActiveServiceAsync(host, null);

        var due = await InvokeAsync(host, new GetRenewableServicesQuery(null, DateTimeOffset.UtcNow.AddDays(14)));

        due.Select(s => s.Id).Should().NotContain([later.ServiceId, undated.ServiceId]);
    }

    /// <summary>Without a client the query answers every client's due services; with one, only theirs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ClientFilter_NarrowsToThatClientAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var first = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(3));
        var second = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(3));
        var horizon = DateTimeOffset.UtcNow.AddDays(14);

        var everyone = await InvokeAsync(host, new GetRenewableServicesQuery(null, horizon));
        var one = await InvokeAsync(host, new GetRenewableServicesQuery(first.ClientId, horizon));

        everyone.Select(s => s.Id).Should().Contain([first.ServiceId, second.ServiceId]);
        one.Select(s => s.Id).Should().Equal(first.ServiceId);
    }

    /// <summary>Dispatches the query on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="query">The query to send.</param>
    /// <returns>The due services.</returns>
    private static async Task<IReadOnlyList<RenewableServiceDto>> InvokeAsync(WebApplicationFactory<Program> host, GetRenewableServicesQuery query)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<RenewableServiceDto>>>(query);
        return result.Value!;
    }
}
