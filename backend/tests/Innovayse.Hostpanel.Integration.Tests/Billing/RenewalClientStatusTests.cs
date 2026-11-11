using Innovayse.Hostpanel.Application.Billing.Commands.ProcessRenewalsCron;
using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Renewal billing for an account that may not be billed: <see cref="RaiseClientRenewalInvoicesCommand"/>
/// refuses a suspended or closed client with its own code, and the daily run leaves such a client's
/// due services out while still invoicing an active client's.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewalClientStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Raising invoices for a suspended or closed client is refused and raises nothing.</summary>
    /// <param name="status">The status the client is in.</param>
    /// <param name="expectedCode">The code the refusal carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ClientStatus.Suspended, HostpanelErrorCodes.CLIENT_SUSPENDED)]
    [InlineData(ClientStatus.Closed, HostpanelErrorCodes.CLIENT_CLOSED)]
    public async Task Raise_ForASuspendedOrClosedClient_IsRefusedAsync(ClientStatus status, string expectedCode)
    {
        var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(5));
        await ClientStatusSeeds.SetAsync(factory, seeded.ClientId, status);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var result = await bus.InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(seeded.ClientId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedCode);
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == seeded.ClientId)).Should().BeFalse();
    }

    /// <summary>The daily run invoices an active client's due service and skips a suspended client's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DailyRun_SkipsASuspendedClientAndStillBillsAnActiveOneAsync()
    {
        var host = RenewalTestSeeds.SuppressedHost(factory);
        var active = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(5));
        var suspended = await RenewalTestSeeds.SeedActiveServiceAsync(host, DateTimeOffset.UtcNow.AddDays(5));
        await ClientStatusSeeds.SetAsync(factory, suspended.ClientId, ClientStatus.Suspended);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == active.ClientId)).Should().Be(1);
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == suspended.ClientId)).Should().BeFalse();
    }
}
