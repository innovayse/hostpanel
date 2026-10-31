using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SetupService;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="SetupServiceCommand"/>: refusing a service that is not
/// Pending, refusing one with no eligible server while keeping the stored details, and setting
/// up a product tied to a server group on that group's server.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SetupServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Setting up a service that is already Active is refused as not Pending.</summary>
    [Fact]
    public async Task SetupService_AnAlreadyActiveServiceRefusesNotPending()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref-1", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result>(
            new SetupServiceCommand(service.Id, "example.com", "jdoe", "Passw0rd!"), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_PENDING);
    }

    /// <summary>
    /// A Pending service with no server registered at all is refused as having none eligible,
    /// rather than crashing the request.
    /// </summary>
    [Fact]
    public async Task SetupService_NoServerRegisteredRefusesNoEligibleServer()
    {
        using var scope = factory.Services.CreateScope();
        var (clientId, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        var selector = scope.ServiceProvider.GetRequiredService<IServerSelector>();
        (await selector.SelectAsync([ServerModule.Cwp7], CancellationToken.None)).Should().BeNull(
            "the test host registers no server, so this refusal is genuine and not a stale fixture");

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result>(
            new SetupServiceCommand(service.Id, "example.com", "jdoe", "Passw0rd!"), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.NO_ELIGIBLE_SERVER);

        // The details stay stored for the next attempt, and the service stays Pending.
        using var readScope = factory.Services.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ClientServices
            .AsNoTracking()
            .SingleAsync(s => s.Id == service.Id);
        stored.Domain.Should().Be("example.com");
        stored.Username.Should().Be("jdoe");
        stored.Status.Should().Be(ServiceStatus.Pending);
    }

    /// <summary>Setting up a service that does not exist refuses <c>SERVICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task SetupService_UnknownServiceRefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new SetupServiceCommand(ServicesTestSeeds.NoSuchServiceId, "example.com", "jdoe", "Passw0rd!"),
            CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// A product in a server group is set up on that group's server, not on the module's default.
    /// It used to pick from every server of the module, so a product tied to one server (for
    /// example the only one with SSH key management) was set up on the default server instead.
    /// </summary>
    [Fact]
    public async Task SetupService_ProductWithServerGroupIsSetUpOnTheGroupsServer()
    {
        await using var host = ProvisioningTestSeeds.WithRecordingProvider(factory, new RecordingProvisioningProvider());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var now = DateTimeOffset.UtcNow;

        // The grouped server is the worse pick on its own (a one-account limit loses the capacity
        // tie-break to an unlimited one, and the other is the default), so only the group can be
        // why it is chosen.
        var outside = NewServer("outside", isDefault: true, maxAccounts: null, now);
        var grouped = NewServer("grouped", isDefault: false, maxAccounts: 1, now);
        context.Servers.AddRange(outside, grouped);
        var group = ServerGroup.Create($"group-{Guid.NewGuid():N}", ServerFillType.LeastFull);
        context.ServerGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        grouped.AssignToGroup(group.Id);

        var (clientId, _) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice: 1m);
        var productGroup = ProductGroup.Create($"setup-group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(productGroup);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(
            productGroup.Id, "Grouped Hosting", null, null, null, "starter", ProductType.SharedHosting, 0m, 0m, now,
            serverGroupId: group.Id);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, now);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);

        try
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var outcome = await bus.InvokeAsync<Result>(
                new SetupServiceCommand(service.Id, "grouped.example.test", "grouped1", "Passw0rd!"), CancellationToken.None);

            outcome.IsSuccess.Should().BeTrue(outcome.ErrorCode);
            var saved = await context.ClientServices.AsNoTracking().SingleAsync(s => s.Id == service.Id);
            saved.ServerId.Should().Be(grouped.Id);
        }
        finally
        {
            // This class shares one database, and the no-server test relies on no eligible CWP7
            // server in it, so the servers seeded here are taken out of selection afterwards.
            // Disabled rather than deleted: the provisioned service still points at one of them.
            await context.Servers
                .Where(s => s.Id == outside.Id || s.Id == grouped.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDisabled, true));
        }
    }

    /// <summary>Builds an enabled CWP7 server that is never reached over the network.</summary>
    /// <param name="label">A label for the name and hostname.</param>
    /// <param name="isDefault">Whether it is the module's default server.</param>
    /// <param name="maxAccounts">The account limit, or <see langword="null"/> for none.</param>
    /// <param name="now">The moment the server is created (UTC).</param>
    /// <returns>The unsaved server.</returns>
    private static Server NewServer(string label, bool isDefault, int? maxAccounts, DateTimeOffset now) =>
        Server.Create(new ServerDetails(
            Name: $"{label}-{Guid.NewGuid():N}",
            Hostname: $"{label}.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.Cwp7,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, "hash"),
            UseSSL: true,
            MaxAccounts: maxAccounts,
            IsDefault: isDefault,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None), now);
}
