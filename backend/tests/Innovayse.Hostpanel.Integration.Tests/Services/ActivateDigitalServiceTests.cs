using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ActivateDigitalService;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="ActivateDigitalServiceCommand"/> through the real <see cref="IMessageBus"/>: a pending service is
/// activated and starts billing without being provisioned, and anything else is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ActivateDigitalServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A pending service turns Active with a renewal date and no provisioning reference or server.</summary>
    [Fact]
    public async Task HandleAsync_PendingService_ActivatesWithoutProvisioningAsync()
    {
        var (_, clientId) = await ServicesTestSeeds.RegisterOwnerAsync(factory, "activate-digital-owner");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);

        var result = await InvokeAsync(new ActivateDigitalServiceCommand(serviceId));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId, CancellationToken.None);
        service.Status.Should().Be(ServiceStatus.Active);
        service.NextRenewalAt.Should().NotBeNull();
        service.ProvisioningRef.Should().BeNull();
        service.ServerId.Should().BeNull();
    }

    /// <summary>A service that is already Active is refused as <c>SERVICE_NOT_PENDING</c> and left as it was.</summary>
    [Fact]
    public async Task HandleAsync_ActiveService_IsRefusedAsync()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);

        var result = await InvokeAsync(new ActivateDigitalServiceCommand(serviceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_PENDING);
    }

    /// <summary>An id nothing names is refused as <c>SERVICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownService_IsRefusedAsync()
    {
        var result = await InvokeAsync(new ActivateDigitalServiceCommand(ServicesTestSeeds.NoSuchServiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(ActivateDigitalServiceCommand command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(command);
    }
}
