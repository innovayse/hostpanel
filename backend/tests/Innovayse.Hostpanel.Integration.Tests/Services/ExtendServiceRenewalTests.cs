using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ExtendServiceRenewal;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="ExtendServiceRenewalCommand"/> through the real <see cref="IMessageBus"/>: the renewal
/// date moves forward to the paid period's end, and sending the same or an earlier period again
/// changes nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ExtendServiceRenewalTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The renewal date becomes the period end, and a repeat of the same command keeps it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_SentTwice_ExtendsOnceAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        var end = start.AddMonths(1);

        var first = await InvokeAsync(host, new ExtendServiceRenewalCommand(seeded.ServiceId, end));
        var second = await InvokeAsync(host, new ExtendServiceRenewalCommand(seeded.ServiceId, end));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        (await NextRenewalAtAsync(host, seeded.ServiceId)).Should().BeCloseTo(end, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>An earlier period end never moves the date back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_EarlierPeriodEnd_LeavesTheDateAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);

        var result = await InvokeAsync(host, new ExtendServiceRenewalCommand(seeded.ServiceId, start.AddDays(-30)));

        result.IsSuccess.Should().BeTrue();
        (await NextRenewalAtAsync(host, seeded.ServiceId)).Should().BeCloseTo(start, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>An id that names no service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnknownService_RefusesNotFoundAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);

        var result = await InvokeAsync(host, new ExtendServiceRenewalCommand(int.MaxValue, TestMoments.Seeded));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Paying the conversion invoice of an awaiting trial marks it converted to paid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExtendServiceRenewal_OfAnAwaitingPaymentTrial_MarksItConvertedToPaidAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        await SetTrialAsync(host, seeded.ServiceId, TrialStatus.AwaitingPayment);

        var result = await InvokeAsync(host, new ExtendServiceRenewalCommand(seeded.ServiceId, start.AddMonths(1)));

        result.IsSuccess.Should().BeTrue();
        (await TrialStatusAsync(host, seeded.ServiceId)).Should().Be(TrialStatus.ConvertedToPaid);
    }

    /// <summary>Extending a still-running trial leaves its trial status alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ExtendServiceRenewal_OfAnActiveTrial_LeavesTheTrialStatusAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var start = TestMoments.Seeded.AddDays(5);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host.Services, start);
        await SetTrialAsync(host, seeded.ServiceId, TrialStatus.Active);

        var result = await InvokeAsync(host, new ExtendServiceRenewalCommand(seeded.ServiceId, start.AddMonths(1)));

        result.IsSuccess.Should().BeTrue();
        (await TrialStatusAsync(host, seeded.ServiceId)).Should().Be(TrialStatus.Active);
    }

    /// <summary>Sets a service's trial status straight in the database.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="status">The status to store.</param>
    /// <returns>A task.</returns>
    private static async Task SetTrialAsync(WebApplicationFactory<Program> host, int serviceId, TrialStatus status)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.ClientServices.Where(s => s.Id == serviceId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.TrialStatus, status), CancellationToken.None);
    }

    /// <summary>Reads a service's trial status back from the database.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The stored trial status.</returns>
    private static async Task<TrialStatus?> TrialStatusAsync(WebApplicationFactory<Program> host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().Where(s => s.Id == serviceId).Select(s => s.TrialStatus).SingleAsync();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="host">The host to dispatch on.</param>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private static async Task<Result> InvokeAsync(WebApplicationFactory<Program> host, ExtendServiceRenewalCommand cmd)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }

    /// <summary>Reads a service's renewal date back from the database.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>The stored renewal date.</returns>
    private static async Task<DateTimeOffset?> NextRenewalAtAsync(WebApplicationFactory<Program> host, int serviceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => s.NextRenewalAt)
            .SingleAsync();
    }
}
