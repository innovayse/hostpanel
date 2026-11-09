using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="GetCancellationStatusQuery"/>, the admin-side read that has
/// no refusal: an unknown or never-cancelled service simply answers "not pending".
/// </summary>
/// <remarks>This query has no HTTP route; it is dispatched directly, the way a cron is.</remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCancellationStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A service with no cancellation request answers "not pending", not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCancellationStatus_NoOpenRequest_AnswersNotPending()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result<CancellationStatusDto>>(
            new GetCancellationStatusQuery(service.Id), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        outcome.Value!.Pending.Should().BeFalse();
    }

    /// <summary>An id that names no service at all also answers "not pending", not a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCancellationStatus_UnknownService_AlsoAnswersNotPending()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result<CancellationStatusDto>>(
            new GetCancellationStatusQuery(ServicesTestSeeds.NoSuchServiceId), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        outcome.Value!.Pending.Should().BeFalse();
    }

    /// <summary>A closed request stored before an open one does not hide the open one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetCancellationStatus_ClosedAndOpenRequests_AnswersTheOpenType()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        var closed = CancellationRequest.Create(service.Id, CancellationType.Immediate, null);
        closed.Close();
        context.CancellationRequests.Add(closed);
        context.CancellationRequests.Add(CancellationRequest.Create(service.Id, CancellationType.EndOfBillingPeriod, null));
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result<CancellationStatusDto>>(
            new GetCancellationStatusQuery(service.Id), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        outcome.Value!.Pending.Should().BeTrue();
        outcome.Value.Type.Should().Be(CancellationType.EndOfBillingPeriod);
    }
}
