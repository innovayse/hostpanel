using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/credit/top-ups</c> against Postgres: a client raises a one-line top-up invoice in their billing currency,
/// inside the limits the admin set, for their own account only. Nothing is credited by raising it; the payment does that.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateMyCreditTopUpTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The invoice is one untaxed line for the amount, Unpaid, flagged as a top-up, owned by the caller and credits nothing yet.</summary>
    [Fact]
    public async Task RaisesAOneLineTopUpInvoiceForTheCaller()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-create");

        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        var invoice = await CreditTestSeeds.InvoiceAsync(factory, invoiceId);
        invoice.ClientId.Should().Be(clientId);
        invoice.IsCreditTopUp.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.Items.Should().ContainSingle();
        invoice.Items[0].Description.Should().Be(Innovayse.Hostpanel.Domain.Billing.Invoice.TopUpDescription);
        invoice.Total.Should().Be(40m);
        invoice.Tax.Should().Be(0m);
        invoice.Credit.Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
    }

    /// <summary>The invoice is in the client's billing currency, the one the credit page reports.</summary>
    [Fact]
    public async Task BillsInTheClientsBillingCurrency()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-currency");
        var credit = await CreditTestSeeds.MyCreditAsync(owner);

        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Currency.Should().Be(credit.GetProperty("currency").GetString());
    }

    /// <summary>The client's own invoice list shows it as a top-up, and no other client can read it.</summary>
    [Fact]
    public async Task TheInvoiceIsTheCallersAndShowsAsATopUp()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-owned");
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "topup-stranger");
        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 40m);

        var own = await owner.GetFromJsonAsync<JsonElement>($"/api/me/invoices/{invoiceId}");
        var foreign = await stranger.GetAsync($"/api/me/invoices/{invoiceId}");

        own.GetProperty("isCreditTopUp").GetBoolean().Should().BeTrue();
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An amount below the minimum or above the maximum is refused with its own code, and no invoice is raised.</summary>
    /// <param name="amount">An amount outside the limits 10 and 100.</param>
    [Theory]
    [InlineData(9.99)]
    [InlineData(100.01)]
    [InlineData(0.5)]
    public async Task RefusesAnAmountOutsideTheLimits(double amount)
    {
        await using var limits = await CreditTestSeeds.LimitsAsync(factory, "10", "100");
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-range");

        var response = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = (decimal)amount });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE);
        await AssertNoInvoiceAsync(clientId);
    }

    /// <summary>Both ends of the range are allowed.</summary>
    /// <param name="amount">The minimum and the maximum.</param>
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AllowsTheLimitsThemselves(double amount)
    {
        await using var limits = await CreditTestSeeds.LimitsAsync(factory, "10", "100");
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-bounds");

        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, (decimal)amount);

        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Total.Should().Be((decimal)amount);
    }

    /// <summary>The limits are the admin's to change: a new maximum takes effect on the next request.</summary>
    [Fact]
    public async Task FollowsTheLimitsTheAdminSets()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-limits");
        await using (await CreditTestSeeds.LimitsAsync(factory, "1", "50"))
        {
            var over = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = 75m });
            over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await using (await CreditTestSeeds.LimitsAsync(factory, "1", "500"))
        {
            (await CreditTestSeeds.TopUpAsync(owner, 75m)).Should().BeGreaterThan(0);
        }
    }

    /// <summary>A contradictory pair (minimum above maximum) falls back to the defaults, so a typo never lifts the limit.</summary>
    [Fact]
    public async Task AContradictoryPairFallsBackToTheDefaults()
    {
        await using var limits = await CreditTestSeeds.LimitsAsync(factory, "900", "10");
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-contradiction");

        var credit = await CreditTestSeeds.MyCreditAsync(owner);

        credit.GetProperty("topUpMinimum").GetDecimal().Should().Be(5m);
        credit.GetProperty("topUpMaximum").GetDecimal().Should().Be(1000m);
    }

    /// <summary>The amount is rounded to the currency's minor unit before it is held to the limits and written on the invoice.</summary>
    [Fact]
    public async Task RoundsTheAmountToTheMinorUnit()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-round");

        var invoiceId = await CreditTestSeeds.TopUpAsync(owner, 12.346m);

        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Total.Should().Be(12.35m);
    }

    /// <summary>An amount of nothing, or less, is a malformed request.</summary>
    /// <param name="amount">Zero or negative.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RefusesANonPositiveAmount(double amount)
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-nonpositive");

        var response = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = (decimal)amount });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route needs a signed-in client, and the body names no client: the account comes from the token.</summary>
    [Fact]
    public async Task NeedsASignedInClient()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/credit/top-ups", new { amount = 20m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client id in the body is ignored: the invoice is raised for the caller, never for the id sent.</summary>
    [Fact]
    public async Task IgnoresAClientIdInTheBody()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-bodyid");
        var other = await BillingTestSeeds.SeedClientAsync(factory);

        var created = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = 20m, clientId = other });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await created.Content.ReadFromJsonAsync<int>();
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).ClientId.Should().Be(clientId);
    }

    /// <summary>
    /// A client holds at most the admin's number of unpaid top-up invoices: the next one is refused with 409
    /// <c>CREDIT_TOP_UP_TOO_MANY_OPEN</c> and raises nothing, and a paid or cancelled one no longer counts.
    /// </summary>
    [Fact]
    public async Task RefusesAnotherTopUpWhileTheOpenOnesReachTheLimit()
    {
        await using var limit = await CreditTestSeeds.MaxOpenAsync(factory, "2");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-open");
        var first = await CreditTestSeeds.TopUpAsync(owner, 20m);
        await CreditTestSeeds.TopUpAsync(owner, 20m);

        var refused = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = 20m });

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(refused)).Should().Be(HostpanelErrorCodes.CREDIT_TOP_UP_TOO_MANY_OPEN);
        await CountTopUpsAsync(clientId, 2);

        (await admin.PostAsync($"/api/billing/{first}/cancel", content: null)).EnsureSuccessStatusCode();
        var allowed = await owner.PostAsJsonAsync("/api/me/credit/top-ups", new { amount = 20m });
        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// An open top-up is an invoice, so moving the client to another currency is refused while it exists: the money it will credit is
    /// in the currency it was raised in, and the balance is kept in the client's.
    /// </summary>
    [Fact]
    public async Task AnOpenTopUp_KeepsTheClientsCurrencyFromChanging()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "topup-currency-lock");
        await CreditTestSeeds.TopUpAsync(owner, 20m);

        var changed = await admin.PutAsJsonAsync($"/api/clients/{clientId}", ClientsTestSeeds.UpdateBody(currency: "AMD"));

        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(changed, HostpanelErrorCodes.CLIENT_CURRENCY_ALREADY_INVOICED);
    }

    /// <summary>Asserts that the client holds exactly <paramref name="expected"/> top-up invoices.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="expected">How many top-up invoices must exist.</param>
    /// <returns>A task that completes once the check has run.</returns>
    private async Task CountTopUpsAsync(int clientId, int expected)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId && i.IsCreditTopUp)).Should().Be(expected);
    }

    /// <summary>Asserts that the client has no invoice at all.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>A task that completes once the check has run.</returns>
    private async Task AssertNoInvoiceAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == clientId)).Should().Be(0);
    }
}
