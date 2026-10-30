using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and authentication helpers shared by every Billing (including Currencies) use-case
/// test file, so the seeding logic exists exactly once rather than once per use case.
/// </summary>
public static class BillingTestSeeds
{
    /// <summary>Shared test password for every seeded client user.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>An id no seeded or created invoice, quote, transaction or billable item ever takes.</summary>
    public const int NoSuchId = 999_999_999;

    /// <summary>An origin the test host's CORS configuration allows a payer to be returned to.</summary>
    public const string AllowedReturnUrl = "https://client.test/payment/result";

    /// <summary>The <c>Retry-After</c> a <see cref="RenderBusyHost"/> sends on a 503: not the default, so the test sees it was read.</summary>
    public const int BusyRetryAfterSeconds = 7;

    /// <summary>
    /// A host that renders one invoice PDF at a time and gives a waiting render one second, so a test
    /// holding the slot through <c>InvoicePdfRenderGate</c> sees the next download refused as busy.
    /// </summary>
    /// <param name="factory">The shared host to derive from.</param>
    /// <returns>The derived host; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> RenderBusyHost(IntegrationTestFactory factory) =>
        factory.WithWebHostBuilder(builder => builder
            .UseSetting("InvoicePdf:MaxConcurrentRenders", "1")
            .UseSetting("InvoicePdf:MaxRenderWaitSeconds", "1")
            .UseSetting("ServiceUnavailable:RetryAfterSeconds", BusyRetryAfterSeconds.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Builds a client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The factory whose admin credentials to mint a token from.</param>
    /// <param name="host">The host to build the client against, or <paramref name="factory"/> itself when null.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/>.</returns>
    public static async Task<HttpClient> AdminClientAsync(
        IntegrationTestFactory factory, WebApplicationFactory<Program>? host = null)
    {
        var client = (host ?? factory).CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Seeds a client row directly against the host's database, bypassing HTTP.</summary>
    /// <param name="host">The host whose database to seed against.</param>
    /// <param name="currency">The ISO 4217 alpha code to stamp the client with, or <see langword="null"/> for the default.</param>
    /// <param name="firstName">The seeded client's first name.</param>
    /// <param name="lastName">The seeded client's last name.</param>
    /// <returns>The new client's primary key.</returns>
    public static async Task<int> SeedClientAsync(
        WebApplicationFactory<Program> host, string? currency = null, string firstName = "Ada", string lastName = "Lovelace")
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var client = Client.Create(
            $"user-{Guid.NewGuid():N}", firstName, lastName, $"{firstName.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        if (currency is not null)
        {
            client.SetCurrency(currency);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Makes sure a currency row exists, so a client stamped with it is billed in it.</summary>
    /// <param name="host">The host whose database to seed against.</param>
    /// <param name="code">The ISO 4217 alpha code.</param>
    /// <param name="numeric">The ISO 4217 numeric code, unique per currency.</param>
    /// <param name="rateToBase">What one unit of the currency is worth in the base currency.</param>
    public static async Task EnsureCurrencyAsync(
        WebApplicationFactory<Program> host, string code, string numeric, decimal rateToBase)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        if (await context.Currencies.AnyAsync(c => c.Code == code, CancellationToken.None))
        {
            return;
        }

        context.Currencies.Add(Currency.Create(code, numeric, string.Empty, " " + code, 2, rateToBase, isBase: false));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Clears the base flag on every stored currency, leaving the installation with no base currency.
    /// Only for a host the calling test owns, since every other test of that database relies on one.
    /// </summary>
    /// <param name="host">The host whose database to change.</param>
    /// <returns>A task that completes once the change is saved.</returns>
    public static async Task RemoveBaseCurrencyAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bases = await context.Currencies.Where(c => c.IsBase).ToListAsync(CancellationToken.None);
        foreach (var currency in bases)
        {
            currency.UnmakeBase();
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Registers a new client user over HTTP, logs in, and waits for the Wolverine handler that
    /// creates the corresponding <see cref="Client"/> row.
    /// </summary>
    /// <param name="factory">The factory to mint a client token from.</param>
    /// <param name="host">The host to register against, or <paramref name="factory"/> itself when null.</param>
    /// <param name="label">Distinguishes the seeded email between callers in the same test.</param>
    /// <returns>The authenticated client and its numeric client id.</returns>
    public static async Task<SignedInClient> RegisterClientAsync(
        IntegrationTestFactory factory, WebApplicationFactory<Program>? host, string label)
    {
        var httpClient = (host ?? factory).CreateClient();
        var email = $"billing-{label}-{Guid.NewGuid():N}@example.test";
        var registered = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Billing",
            lastName = label,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetString()!;

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var clientId = 0;
        for (var i = 0; i < 10 && clientId == 0; i++)
        {
            await Task.Delay(300);
            var me = await httpClient.GetAsync("/api/clients/me");
            if (me.IsSuccessStatusCode)
            {
                var json = await me.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("id", out var idProp))
                {
                    clientId = idProp.GetInt32();
                }
            }
        }

        clientId.Should().BeGreaterThan(0, "the Wolverine handler should have created the client record");
        return new SignedInClient(httpClient, clientId, userId, email);
    }

    /// <summary>Registers a client user who owns nothing: the "somebody else" of an ownership test.</summary>
    /// <param name="factory">The factory to mint a client token from.</param>
    /// <param name="host">The host to register against, or <paramref name="factory"/> itself when null.</param>
    /// <param name="label">Distinguishes the seeded email between callers in the same test.</param>
    /// <returns>An authenticated client for the stranger.</returns>
    public static async Task<HttpClient> RegisterStrangerAsync(
        IntegrationTestFactory factory, WebApplicationFactory<Program>? host, string label)
    {
        var httpClient = (host ?? factory).CreateClient();
        var email = $"billing-{label}-{Guid.NewGuid():N}@example.test";
        var registered = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Billing",
            lastName = label,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Creates an invoice for <paramref name="clientId"/> as the admin and returns its id.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="clientId">The client to invoice.</param>
    /// <param name="unitPrice">The single line item's price.</param>
    /// <param name="isDraft">Whether the invoice should be created as a Draft rather than Unpaid.</param>
    /// <returns>The new invoice's primary key.</returns>
    /// <remarks>
    /// The invoice's currency is not a field on <c>CreateInvoiceCommand</c>; it comes from the
    /// invoiced client's own stamped currency, resolved through <c>IPayerCurrencyResolver</c>. A
    /// caller who needs a specific currency stamps the client first, through
    /// <see cref="SeedClientAsync"/>.
    /// </remarks>
    public static async Task<int> CreateInvoiceAsync(
        HttpClient admin, int clientId, decimal unitPrice, bool isDraft = false)
    {
        var created = await admin.PostAsJsonAsync("/api/billing", new
        {
            clientId,
            dueDate = DateTimeOffset.UtcNow.AddDays(14),
            isDraft,
            items = new[] { new { description = "Hosting", unitPrice, quantity = 1 } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Records a manual payment for the whole invoice as the admin, which marks it paid.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="invoiceId">The invoice to settle.</param>
    /// <param name="amount">The payment amount, the invoice's total.</param>
    public static async Task MarkPaidAsync(HttpClient admin, int invoiceId, decimal amount)
    {
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"manual-{Guid.NewGuid():N}",
            amount,
            fees = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Creates a quote for <paramref name="clientId"/> as the admin and returns its id.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="clientId">The client to quote.</param>
    /// <param name="unitPrice">The single line item's price.</param>
    /// <returns>The new quote's primary key.</returns>
    public static async Task<int> CreateQuoteAsync(HttpClient admin, int clientId, decimal unitPrice)
    {
        var created = await admin.PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId,
            subject = "Website redesign",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[] { new { description = "Design", unitPrice, quantity = 1 } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Creates a transaction for <paramref name="clientId"/> as the admin and returns its id.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="clientId">The client to credit or debit.</param>
    /// <param name="amountIn">The amount credited to the client.</param>
    /// <param name="addToCredit">Whether the amount should also be added to the client's credit balance.</param>
    /// <returns>The new transaction's primary key.</returns>
    public static async Task<int> CreateTransactionAsync(
        HttpClient admin, int clientId, decimal amountIn, bool addToCredit = false)
    {
        var created = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId,
            date = DateTimeOffset.UtcNow,
            description = "Manual top-up",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn,
            amountOut = 0m,
            fees = 0m,
            addToCredit,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Stamps the client's card payment gateway customer id, the only way to make their saved payment methods reachable.</summary>
    /// <param name="host">The host whose database the client lives in.</param>
    /// <param name="clientId">The client to stamp.</param>
    /// <param name="customerId">The card payment gateway customer id to write.</param>
    public static async Task StampCardCustomerAsync(WebApplicationFactory<Program> host, int clientId, string customerId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await context.Clients.FirstAsync(c => c.Id == clientId, CancellationToken.None);
        client.SetCardGatewayCustomerId(customerId);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Adds credit to a client's balance directly against the host's database.</summary>
    /// <param name="host">The host whose database the client lives in.</param>
    /// <param name="clientId">The client to credit.</param>
    /// <param name="amount">The credit to add.</param>
    public static async Task GiveCreditAsync(WebApplicationFactory<Program> host, int clientId, decimal amount)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await context.Clients.FirstAsync(c => c.Id == clientId, CancellationToken.None);
        client.AddCredit(amount);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads a client's credit balance from the host's database.</summary>
    /// <param name="host">The host whose database the client lives in.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The current credit balance.</returns>
    public static async Task<decimal> CreditBalanceOfAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => c.CreditBalance)
            .FirstAsync(CancellationToken.None);
    }

    /// <summary>Reads the <c>type</c> field of a refusal answered by <c>BaseApiController</c>.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The error code.</returns>
    public static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("type").GetString();
    }
}
