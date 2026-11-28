using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for the <c>StartMyTrial</c> use case: <c>POST /api/me/services/trial</c> resolves the
/// caller inside the handler and starts the free trial of a product on that account, free of charge.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartMyTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Starting the trial creates an active trial service at the cycle price, with no order and no invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_CreatesAnActiveTrialServiceWithoutOrderOrInvoiceAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 14, monthlyPrice: 4.5m);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var serviceId = await response.Content.ReadFromJsonAsync<int>();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var service = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == serviceId);
        service.ClientId.Should().Be(clientId);
        service.TrialStatus.Should().Be(TrialStatus.Active);
        service.TrialEndsAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromMinutes(5));
        service.FirstPaymentAmount.Should().Be(0m);
        service.RecurringAmount.Should().Be(4.5m);
        (await context.Orders.AnyAsync(o => o.ClientId == clientId)).Should().BeFalse();
        (await context.Invoices.AnyAsync(i => i.ClientId == clientId)).Should().BeFalse();
    }

    /// <summary>The client list and the admin list and detail carry the trial; an ordinary service carries nulls.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_ThenReads_CarryTheTrialFieldsAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 14, monthlyPrice: 4.5m);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-dto");
        var started = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });
        var serviceId = await started.Content.ReadFromJsonAsync<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var (_, otherProduct) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, 2m);
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var now = DateTimeOffset.UtcNow;
            var plain = Innovayse.Hostpanel.Domain.Services.ClientService.Create(clientId, otherProduct, BillingCycle.Monthly, now);
            context.ClientServices.Add(plain);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/me/services");
        var trial = mine.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == serviceId);
        trial.GetProperty("trialStatus").ToString().Should().Be("Active");
        trial.GetProperty("trialDaysLeft").GetInt32().Should().Be(14);
        trial.GetProperty("trialEndsAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromMinutes(5));
        var ordinary = mine.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() != serviceId);
        ordinary.GetProperty("trialStatus").ValueKind.Should().Be(JsonValueKind.Null);
        ordinary.GetProperty("trialEndsAt").ValueKind.Should().Be(JsonValueKind.Null);
        ordinary.GetProperty("trialDaysLeft").ValueKind.Should().Be(JsonValueKind.Null);

        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/services/{serviceId}");
        detail.GetProperty("trialStatus").ToString().Should().Be("Active");
        detail.GetProperty("trialDaysLeft").GetInt32().Should().Be(14);
        detail.GetProperty("trialEndsAt").ValueKind.Should().Be(JsonValueKind.String);
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/services?page=1&pageSize=50&clientId={clientId}");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        var listed = items.Single(e => e.GetProperty("id").GetInt32() == serviceId);
        listed.GetProperty("trialStatus").ToString().Should().Be("Active");
        listed.GetProperty("trialDaysLeft").GetInt32().Should().Be(14);
        items.Single(e => e.GetProperty("id").GetInt32() != serviceId).GetProperty("trialStatus").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>The public catalogue shows whether a product has a trial and its length, 0 days when it has none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Catalogue_ShowsTheTrialOfAProductAsync()
    {
        var withTrial = await SeedProductAsync(trialEnabled: true, days: 9, monthlyPrice: 3m);
        var without = await SeedProductAsync(trialEnabled: false, days: 9, monthlyPrice: 3m);

        var catalogue = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/public/products");

        var on = catalogue.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == withTrial);
        on.GetProperty("trialEnabled").GetBoolean().Should().BeTrue();
        on.GetProperty("trialDays").GetInt32().Should().Be(9);
        var off = catalogue.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == without);
        off.GetProperty("trialEnabled").GetBoolean().Should().BeFalse();
        off.GetProperty("trialDays").GetInt32().Should().Be(0);
    }

    /// <summary>A trial of a digital product has no hosting account to set up, so it is activated at once and renews at the trial end.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_OnADigitalProduct_ActivatesTheServiceAtOnceAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 14, monthlyPrice: 4.5m, type: ProductType.Digital);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-digital");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await ServiceAsync(await response.Content.ReadFromJsonAsync<int>());
        service.Status.Should().Be(ServiceStatus.Active);
        service.TrialStatus.Should().Be(TrialStatus.Active);
        service.NextRenewalAt.Should().Be(service.TrialEndsAt);
    }

    /// <summary>A hosting trial stays Pending until the client sets it up like any ordered service.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_OnAHostingProduct_LeavesTheServicePendingAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 14, monthlyPrice: 4.5m);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-hosting");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await ServiceAsync(await response.Content.ReadFromJsonAsync<int>());
        service.Status.Should().Be(ServiceStatus.Pending);
        service.TrialStatus.Should().Be(TrialStatus.Active);
    }

    /// <summary>A second start of the same product's trial is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_Twice_IsRefusedAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 7, monthlyPrice: 3m);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-twice");
        (await client.PostAsJsonAsync("/api/me/services/trial", new { productId })).StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(second, HostpanelErrorCodes.ORDER_TRIAL_ALREADY_USED);
    }

    /// <summary>A product with the trial off answers 409 <c>PRODUCT_TRIAL_NOT_AVAILABLE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_OnAProductWithoutATrial_IsRefusedAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: false, days: 7, monthlyPrice: 3m);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-off");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_TRIAL_NOT_AVAILABLE);
    }

    /// <summary>An inactive product cannot be tried.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_OnAnInactiveProduct_IsRefusedAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 7, monthlyPrice: 3m, active: false);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-inactive");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_AVAILABLE);
    }

    /// <summary>A suspended or closed account cannot start a trial.</summary>
    /// <param name="status">The status the account is in.</param>
    /// <param name="expectedCode">The code the refusal carries.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ClientStatus.Suspended, HostpanelErrorCodes.CLIENT_SUSPENDED)]
    [InlineData(ClientStatus.Closed, HostpanelErrorCodes.CLIENT_CLOSED)]
    public async Task Start_ByASuspendedOrClosedAccount_IsRefusedAsync(ClientStatus status, string expectedCode)
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 7, monthlyPrice: 3m);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, $"start-trial-status-{status}");
        await ClientStatusSeeds.SetAsync(factory, clientId, status);

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, expectedCode);
    }

    /// <summary>A user linked without <c>PlaceNewOrders</c> is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_ByAUserWithoutThePermission_IsRefusedAsync()
    {
        var productId = await SeedProductAsync(trialEnabled: true, days: 7, monthlyPrice: 3m);
        var (_, ownerId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "start-trial-denied", ownerId, ClientPermission.ViewEmails);

        var response = await user.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_ByAnonymous_IsRefusedAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/services/trial", new { productId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive product id is a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithProductIdZero_IsAValidationErrorAsync()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-zero");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>With several cycles priced, the trial runs on the cheapest one and stores its price.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_OnAProductWithSeveralCycles_UsesTheCheapestAsync()
    {
        var productId = await SeedProductAsync(true, 7, null, prices:
        [
            ("USD", BillingCycle.Monthly, 10m),
            ("USD", BillingCycle.Quarterly, 6m),
            ("USD", BillingCycle.Annual, 8m),
        ]);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-cheapest");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await ServiceAsync(await response.Content.ReadFromJsonAsync<int>());
        service.BillingCycle.Should().Be(BillingCycle.Quarterly);
        service.RecurringAmount.Should().Be(6m);
    }

    /// <summary>A cycle priced at zero counts as unpriced and is skipped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_SkipsACyclePricedAtZeroAsync()
    {
        var productId = await SeedProductAsync(true, 7, null, prices:
        [
            ("USD", BillingCycle.Monthly, 0m),
            ("USD", BillingCycle.Annual, 8m),
        ]);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "start-trial-zero-skip");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = await ServiceAsync(await response.Content.ReadFromJsonAsync<int>());
        service.BillingCycle.Should().Be(BillingCycle.Annual);
        service.RecurringAmount.Should().Be(8m);
    }

    /// <summary>A product priced only in another currency, or only at zero, cannot be tried and records nothing.</summary>
    /// <param name="currency">The currency of the only price.</param>
    /// <param name="amount">The only price.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("EUR", 5)]
    [InlineData("USD", 0)]
    public async Task Start_WithoutAUsablePrice_IsRefusedAndRecordsNothingAsync(string currency, int amount)
    {
        var productId = await SeedProductAsync(true, 7, null, prices: [(currency, BillingCycle.Monthly, (decimal)amount)]);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, $"start-trial-noprice-{currency}-{amount}");

        var response = await client.PostAsJsonAsync("/api/me/services/trial", new { productId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_PRICE_UNAVAILABLE);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ClientServices.AnyAsync(s => s.ClientId == clientId)).Should().BeFalse();
        (await context.ClientProductTrials.AnyAsync(r => r.ClientId == clientId)).Should().BeFalse();
    }

    /// <summary>Reads a service back.</summary>
    /// <param name="id">The service id.</param>
    /// <returns>The service.</returns>
    private async Task<Innovayse.Hostpanel.Domain.Services.ClientService> ServiceAsync(int id)
    {
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == id);
    }

    /// <summary>Seeds an active product with the trial configured and a USD monthly price.</summary>
    /// <param name="trialEnabled">Whether the trial is on.</param>
    /// <param name="days">The trial length.</param>
    /// <param name="monthlyPrice">The monthly USD price.</param>
    /// <param name="active">Whether the product is on sale.</param>
    /// <param name="type">The product type.</param>
    /// <param name="prices">Cycle prices to add on top of the monthly one.</param>
    /// <returns>The product id.</returns>
    private async Task<int> SeedProductAsync(bool trialEnabled, int days, decimal? monthlyPrice, bool active = true, ProductType type = ProductType.SharedHosting, IReadOnlyList<(string Currency, BillingCycle Cycle, decimal Amount)>? prices = null)
    {
        using var scope = factory.Services.CreateScope();
        var (_, productId) = await ServicesTestSeeds.SeedClientAndProductAsync(scope, monthlyPrice);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var product = await context.Products.FirstAsync(p => p.Id == productId);
        foreach (var (currency, cycle, amount) in prices ?? [])
        {
            product.SetPrice(currency, cycle, amount);
        }

        typeof(Product).GetProperty(nameof(Product.Type))!.SetValue(product, type);
        product.ConfigureTrial(new ProductTrial(trialEnabled, days, true, [3, 1], 3));
        if (!active)
        {
            product.Deactivate();
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return productId;
    }
}
