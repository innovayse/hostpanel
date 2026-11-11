using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CancelService;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="CancelServiceCommand"/>: refusing a second open cancellation
/// request, accepting a new one beside a closed one, refusing an unknown or terminated service, and
/// acting on the request (an immediate one terminates, an end-of-period one marks the service to end
/// with its cycle).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A second cancellation request while one is already open is refused, not duplicated.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_ASecondOpenRequestIsRefused()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, "Changed my mind"), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, null), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.ErrorCode.Should().Be(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
    }

    /// <summary>A closed earlier request does not hide an open one: a second open request is still refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_AClosedRequestDoesNotHideAnOpenOne()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var closed = CancellationRequest.Create(serviceId, CancellationType.EndOfBillingPeriod, "Earlier request");
        closed.Close();
        context.CancellationRequests.Add(closed);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var first = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, null), CancellationToken.None);
        var second = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, null), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.ErrorCode.Should().Be(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
    }

    /// <summary>Cancelling a service that does not exist refuses <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_UnknownServiceRefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(ServicesTestSeeds.NoSuchServiceId, CancellationType.Immediate, null), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>A terminated service has nothing left to cancel: refused, and no request is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_TerminatedServiceIsRefused()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Terminated);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.Immediate, null), CancellationToken.None);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_ALREADY_TERMINATED);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CancellationRequests.AnyAsync(r => r.ServiceId == serviceId, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>An end-of-period request marks the service to end with its cycle, with the reason, and leaves it running.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_EndOfBillingPeriodMarksTheServiceToEndWithItsCycle()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.EndOfBillingPeriod, "Moving elsewhere"), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        service.Status.Should().Be(ServiceStatus.Active);
        service.AutoTerminateEndOfCycle.Should().BeTrue();
        service.AutoTerminateReason.Should().Be("Moving elsewhere");
        (await context.CancellationRequests.AnyAsync(
            r => r.ServiceId == serviceId && r.Status == CancellationStatus.Open, CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>An immediate request terminates the service through the terminate use case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelService_ImmediateTerminatesTheService()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new CancelServiceCommand(serviceId, CancellationType.Immediate, "Done with it"), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId, CancellationToken.None);
        service.Status.Should().Be(ServiceStatus.Terminated);
    }
}
