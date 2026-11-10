using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/clients/{id}/credit/adjustments</c> against Postgres: staff add credit to a client's balance or take it off with
/// a reason; the balance and a credit-only history line change in one save, the client is mailed the new balance, and a
/// deduction never takes more than the balance holds.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AdjustClientCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Adding credit raises the balance and writes one credit-only line with the reason, which moves no cash.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Add_RaisesTheBalanceAndRecordsTheLine()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction = nameof(CreditAdjustment.Add),
            amount = 25m,
            reason = "Goodwill after the outage",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(25m);
        var line = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.AdminAdd);
        line.CreditChange.Should().Be(25m);
        line.BalanceAfter.Should().Be(25m);
        line.Description.Should().Be("Goodwill after the outage");
        line.MovesMoney.Should().BeFalse();
    }

    /// <summary>Taking credit lowers the balance and the line carries the negative change and the balance it left.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deduct_LowersTheBalanceAndRecordsTheLine()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 40m, reason = "Start" });

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction = nameof(CreditAdjustment.Deduct),
            amount = 15m,
            reason = "Wrong client",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(25m);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        history.Select(t => (t.CreditMovement, t.CreditChange, t.BalanceAfter)).Should().BeEquivalentTo(new (CreditMovementType?, decimal?, decimal?)[]
        {
            (CreditMovementType.AdminAdd, 40m, 40m),
            (CreditMovementType.AdminDeduct, -15m, 25m),
        });
    }

    /// <summary>A deduction of more than the balance holds is refused with its code and changes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Deduct_MoreThanTheBalance_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 5m);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction = "Deduct",
            amount = 6m,
            reason = "Too much",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(5m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
    }

    /// <summary>The reason is required, the amount must be positive and the direction must be one of the two.</summary>
    /// <param name="direction">The direction sent.</param>
    /// <param name="amount">The amount sent.</param>
    /// <param name="reason">The reason sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Add", 10, "")]
    [InlineData("Add", 10, "   ")]
    [InlineData("Add", 0, "Because")]
    [InlineData("Deduct", -3, "Because")]
    [InlineData("Sideways", 10, "Because")]
    public async Task RefusesAMalformedAdjustment(string direction, double amount, string reason)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction,
            amount = (decimal)amount,
            reason,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>A reason longer than the history line holds is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAReasonThatIsTooLong()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction = "Add",
            amount = 5m,
            reason = new string('x', 501),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An id that names no client is refused with 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnUnknownClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{BillingTestSeeds.NoSuchId}/credit/adjustments", new
        {
            direction = "Add",
            amount = 5m,
            reason = "Nobody",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>The route is staff's: a client, or nobody, is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToClientsAndAnonymousCallers()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "adjust-forbidden");
        var body = new { direction = "Add", amount = 5m, reason = "Self service" };

        var byClient = await owner.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", body);
        var anonymous = await factory.CreateClient().PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", body);

        byClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>The client is mailed the change and the new balance, in the same moment the staff adjustment lands.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MailsTheClientTheNewBalance()
    {
        var sender = new FakeEmailSender();
        var host = BankTransferTestSeeds.HostWithMail(factory, sender);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (_, clientId, _, email) = await BillingTestSeeds.RegisterClientAsync(factory, host, "adjust-mail");

        await BankTransferTestSeeds.RunTrackedAsync(host, async () =>
            await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 12m, reason = "Promo" }));

        var mail = sender.Sent.Where(m => m.To == email && m.Subject.StartsWith("Your credit balance is now")).Should().ContainSingle().Subject;
        mail.Body.Should().Contain("12");
        mail.Body.Should().NotContain("Promo", "the reason is for staff, the client reads the change and the balance");
    }

    /// <summary>Two adjustments at once both land: the later save finds the balance moved and the retry is not needed by the caller's data.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoAdjustmentsAtOnce_NeverLoseOne()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var otherAdmin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var results = await Task.WhenAll(
            admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 10m, reason = "One" }),
            otherAdmin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 20m, reason = "Two" }));
        foreach (var (response, amount) in new[] { (results[0], 10m), (results[1], 20m) })
        {
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                response.StatusCode.Should().Be(HttpStatusCode.Conflict);
                (await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount, reason = "Retry" }))
                    .StatusCode.Should().Be(HttpStatusCode.NoContent);
            }
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(30m);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        history.Sum(t => t.CreditChange!.Value).Should().Be(30m);
        history.Last().BalanceAfter.Should().Be(30m);
    }
}
