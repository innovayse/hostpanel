using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>Integration tests for <c>GET /api/services/renewals/upcoming</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListUpcomingServiceRenewalsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds an active service renewing in the given number of days.</summary>
    /// <param name="host">The clocked host whose moment the days count from.</param>
    /// <param name="inDays">Days from the host clock to its next renewal.</param>
    /// <param name="autoRenew">Whether it renews automatically.</param>
    /// <returns>The service id.</returns>
    private async Task<int> SeedAsync(ClockedHost host, int inDays, bool autoRenew)
    {
        var id = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.SingleAsync(s => s.Id == id);
        service.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m, RecurringAmount: 5m,
            NextRenewalAt: host.Clock.GetUtcNow().AddDays(inDays), PaymentMethod: null, PromotionCode: null, SubscriptionId: null));
        service.SetAutoRenew(autoRenew);
        await context.SaveChangesAsync(CancellationToken.None);
        return id;
    }

    /// <summary>Reads the ids of the services on a page of the endpoint.</summary>
    /// <param name="host">The clocked host the endpoint runs on.</param>
    /// <param name="window">The window name on the wire.</param>
    /// <returns>The service ids and the flag of each, by id.</returns>
    private async Task<Dictionary<int, bool>> ListAsync(ClockedHost host, string window)
    {
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var response = await admin.GetAsync($"/api/services/renewals/upcoming?window={window}&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items").EnumerateArray()
            .ToDictionary(e => e.GetProperty("id").GetInt32(), e => e.GetProperty("autoRenew").GetBoolean());
    }

    /// <summary>A wider window brings in later renewals, and a service on manual billing is listed with its switch off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Windows_BringInLaterRenewals_AndKeepManualBillingVisible()
    {
        using var host = new ClockedHost(factory);
        var soon = await SeedAsync(host, 10, autoRenew: true);
        var later = await SeedAsync(host, 45, autoRenew: false);
        var farOff = await SeedAsync(host, 75, autoRenew: true);

        var in30 = await ListAsync(host, "Next30Days");
        var in60 = await ListAsync(host, "Next60Days");
        var in90 = await ListAsync(host, "Next90Days");

        in30.Keys.Should().Contain(soon).And.NotContain(later).And.NotContain(farOff);
        in60.Keys.Should().Contain([soon, later]).And.NotContain(farOff);
        in90.Keys.Should().Contain([soon, later, farOff]);
        in60[later].Should().BeFalse("a service on manual billing is listed so staff can switch it back on");
        in60[soon].Should().BeTrue();
    }

    /// <summary>A terminated service is not listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InactiveServices_AreLeftOut()
    {
        using var host = new ClockedHost(factory);
        var id = await SeedAsync(host, 5, autoRenew: true);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var service = await context.ClientServices.SingleAsync(s => s.Id == id);
            service.Terminate(host.Clock.GetUtcNow(), hostingAlreadyUpdated: true);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        (await ListAsync(host, "Next30Days")).Keys.Should().NotContain(id);
    }

    /// <summary>A window that is not one of the three is refused with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownWindow_Returns400()
    {
        var admin = await ServicesTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/services/renewals/upcoming?window=45");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Without authentication the call is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutAuth_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/services/renewals/upcoming");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
