using System.Net;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Commands.UpdateDiskUsage;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>POST /api/reports/disk-usage/update</c> against Postgres: refreshes the cached disk usage
/// report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDiskUsageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Refreshing disk usage with no control panel servers configured still answers 200.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefreshesTheCache()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An enabled server whose panel cannot report usage is listed as a failure, not left out silently.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnsupportedPanelServer_ListsItAsAFailureAsync()
    {
        var name = $"cwp-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Servers.Add(Server.Create(new ServerDetails(
            Name: name,
            Hostname: "usage.example.test",
            IpAddress: null,
            AssignedIpAddresses: null,
            Module: ServerModule.Cwp7,
            Username: "api",
            Credentials: new ServerCredentials("secret", null, null),
            UseSSL: true,
            MaxAccounts: null,
            IsDefault: false,
            IsDisabled: false,
            MonthlyCost: 0m,
            Datacenter: null,
            ServerStatusAddress: null,
            NameServers: ServerNameServers.None,
            AcceptsSelfSignedCertificate: false), TestMoments.Seeded));
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result<DiskUsageDto>>(new UpdateDiskUsageCommand(), CancellationToken.None);

        result.Value!.Failures.Should().Contain(new DiskUsageFailureDto(name, DiskUsageFailureReason.UnsupportedModule));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "update-disk-usage");

        var response = await client.PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
