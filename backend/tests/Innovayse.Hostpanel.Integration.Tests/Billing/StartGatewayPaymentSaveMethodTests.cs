using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Starting a hosted-gateway payment with the choice to keep the payment method (<c>StartGatewayPaymentCommand</c>
/// with <c>SavePaymentMethod</c>, reached through <c>POST /api/me/invoices/{id}/gateway-payment/start</c> and
/// <c>POST /api/orders/{id}/gateway-payment/start</c>) against Postgres, with a gateway that offers stored credentials:
/// the consent is recorded with its evidence together with the registered payment, the gateway customer exists before
/// the payment is registered, and every caller who may not save a method is refused before the gateway is asked.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartGatewayPaymentSaveMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Asking to keep the method registers the payment with the flag, records one consent keyed by the gateway order with
    /// the wording version and the request's own browser, leaves it without a method, and ensures the customer once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSave_RecordsTheConsentAndEnsuresTheCustomer()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-start");
        signedIn.Client.DefaultRequestHeaders.UserAgent.ParseAdd("EvidenceBrowser/1.0");

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.Instructions.Should().ContainSingle().Which.SavePaymentMethod.Should().BeTrue();
        var gatewayOrderId = (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId;
        var consent = (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().ContainSingle().Subject;
        consent.SessionId.Should().Be(gatewayOrderId);
        consent.ConsentTextVersion.Should().Be(SavingGatewayScenario.ConsentTextVersion);
        consent.UserAgent.Should().Be("EvidenceBrowser/1.0");
        consent.IpAddress.Should().NotBeNull();
        consent.MethodRef.Should().BeNull("the method is bound when the payment is completed");
        consent.IsActive.Should().BeFalse();
        scenario.Plugin.Customers.Should().ContainSingle();
        (await scenario.CustomerCountAsync(signedIn.ClientId)).Should().Be(1);
    }

    /// <summary>The address and browser kept with the consent are the request's own: whatever the body says for them is overwritten.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSave_KeepsTheEvidenceFromTheRequestNotTheBody()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-evidence");
        signedIn.Client.DefaultRequestHeaders.UserAgent.ParseAdd("RealBrowser/2.0");

        var response = await signedIn.Client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new
            {
                module = scenario.Module,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                savePaymentMethod = true,
                consentTextVersion = SavingGatewayScenario.ConsentTextVersion,
                ipAddress = "203.0.113.99",
                userAgent = "Forged/9.9",
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var consent = (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().ContainSingle().Subject;
        consent.UserAgent.Should().Be("RealBrowser/2.0");
        consent.IpAddress.Should().NotBe("203.0.113.99", "the address is the connection's, never the body's");
    }

    /// <summary>A start that does not ask to keep the method registers the payment without the flag and records no consent and no customer.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithoutTheFlag_RecordsNothing()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-none");

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: false, consentTextVersion: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.Instructions.Should().ContainSingle().Which.SavePaymentMethod.Should().BeFalse();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
        scenario.Plugin.Customers.Should().BeEmpty();
        (await scenario.CustomerCountAsync(signedIn.ClientId)).Should().Be(0);
    }

    /// <summary>Starting twice for the same client keeps one gateway customer and records a consent for each registered payment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveTwice_KeepsOneCustomerAndOneConsentPerPayment()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-twice");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var secondInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, signedIn.ClientId, 12m);

        (await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.StartAsync(signedIn.Client, secondInvoiceId, savePaymentMethod: true)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await scenario.CustomerCountAsync(signedIn.ClientId)).Should().Be(1);
        scenario.Plugin.Customers.Should().ContainSingle("the plugin is asked for the customer only the first time");
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().HaveCount(2);
    }

    /// <summary>A module without the stored credential capability is refused before the gateway is asked, and nothing is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveAtAModuleWithoutStoredCredentials_IsRefused()
    {
        using var scenario = new SavingGatewayScenario(factory, storedCredentials: false);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-unsupported");

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        scenario.Plugin.Instructions.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
        (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId.Should().BeNull();
    }

    /// <summary>Asking to keep the method without the consent wording version is a validation error and reaches nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveWithoutTheConsentTextVersion_IsRefused()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-noversion");

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true, consentTextVersion: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be("VALIDATION_ERROR");
        scenario.Plugin.Instructions.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
    }

    /// <summary>A consent wording version longer than the consent can keep is a validation error, so the evidence is never cut short.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveAndATooLongConsentTextVersion_IsRefused()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-longversion");

        var response = await scenario.StartAsync(
            signedIn.Client, invoiceId, savePaymentMethod: true, consentTextVersion: new string('v', CardChargeConsent.MaxTextVersionLength + 1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be("VALIDATION_ERROR");
        scenario.Plugin.Instructions.Should().BeEmpty();
    }

    /// <summary>A wording version nobody asked to keep is not validated: a payment that saves nothing has no consent to describe.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithoutSaveAndATooLongConsentTextVersion_StillRegistersThePayment()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-ignored");

        var response = await scenario.StartAsync(
            signedIn.Client, invoiceId, savePaymentMethod: false, consentTextVersion: new string('v', CardChargeConsent.MaxTextVersionLength + 1));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
    }

    /// <summary>A client whose card processing is switched off cannot save a method: refused before the customer or the payment exist.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveByAClientWithCardProcessingDisabled_IsRefused()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-disabled");
        await ClientSettingsSeeds.SetAsync(factory, signedIn.ClientId, disableCcProcessing: true);

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED);
        scenario.Plugin.Instructions.Should().BeEmpty();
        scenario.Plugin.Customers.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
    }

    /// <summary>A gateway that cannot create the customer refuses the start as unavailable and leaves no payment, consent or customer row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveWhenTheGatewayCannotCreateTheCustomer_IsUnavailable()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-outage");
        scenario.Plugin.EnsureCustomerThrows = new TimeoutException("The gateway did not answer.");

        var response = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        scenario.Plugin.Instructions.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
        (await scenario.CustomerCountAsync(signedIn.ClientId)).Should().Be(0);
    }

    /// <summary>A guest, who has no account to consent for, cannot ask to keep the method: 401, and the gateway is never asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveByAGuest_IsUnauthorized()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await scenario.Host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new
            {
                module = scenario.Module,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                paymentToken,
                savePaymentMethod = true,
                consentTextVersion = SavingGatewayScenario.ConsentTextVersion,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        scenario.Plugin.Instructions.Should().BeEmpty();
        (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId.Should().BeNull();
    }

    /// <summary>A guest who does not ask to keep the method still pays through the order route, as before.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithoutSaveByAGuest_StillRegistersThePayment()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await scenario.Host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new { module = scenario.Module, returnUrl = BillingTestSeeds.AllowedReturnUrl, paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.Instructions.Should().ContainSingle().Which.SavePaymentMethod.Should().BeFalse();
    }

    /// <summary>A signed-in client who holds the order's token but is not the invoice's client cannot consent on it: the invoice is not found.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveByAClientWhoDoesNotOwnTheInvoice_IsNotFound()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var stranger = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "save-stranger");

        var response = await stranger.Client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/start",
            new
            {
                module = scenario.Module,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                paymentToken,
                savePaymentMethod = true,
                consentTextVersion = SavingGatewayScenario.ConsentTextVersion,
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        scenario.Plugin.Instructions.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(stranger.ClientId)).Should().BeEmpty();
        (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId.Should().BeNull();
    }

    /// <summary>Starts the gateway payment of an order's invoice through the route that is open to guests, as the given caller.</summary>
    /// <param name="caller">The caller's HTTP client, served by the scenario's host.</param>
    /// <param name="scenario">The scenario whose module to pay at.</param>
    /// <param name="order">The seeded order, its payment token and its invoice.</param>
    /// <param name="savePaymentMethod">Whether the caller asks to keep the method they pay with.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> StartOrderPaymentAsync(
        HttpClient caller, SavingGatewayScenario scenario, SeededInvoicedOrder order, bool savePaymentMethod) =>
        caller.PostAsJsonAsync(
            $"/api/orders/{order.OrderId}/gateway-payment/start",
            new
            {
                module = scenario.Module,
                returnUrl = BillingTestSeeds.AllowedReturnUrl,
                paymentToken = order.PaymentToken,
                savePaymentMethod,
                consentTextVersion = savePaymentMethod ? SavingGatewayScenario.ConsentTextVersion : null,
            });

    /// <summary>
    /// A user linked to the account without the permission to view and pay invoices cannot keep a method for the account
    /// through the route that is open to guests: refused before the customer or the payment exist.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveByALinkedUserWithoutViewPayInvoices_IsForbidden()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var order = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var clientId = (await scenario.InvoiceAsync(order.InvoiceId)).ClientId;
        var linked = await scenario.SignInLinkedUserAsync("save-noperm", clientId, ClientPermission.ViewEmails);

        var response = await StartOrderPaymentAsync(linked, scenario, order, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        scenario.Plugin.Instructions.Should().BeEmpty();
        scenario.Plugin.Customers.Should().BeEmpty();
        (await scenario.ConsentsOfAsync(clientId)).Should().BeEmpty();
        (await scenario.InvoiceAsync(order.InvoiceId)).GatewayOrderId.Should().BeNull();
    }

    /// <summary>The same linked user still pays through the route when they do not ask to keep the method: the permission guards the saving only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithoutSaveByALinkedUserWithoutViewPayInvoices_StillRegistersThePayment()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var order = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var clientId = (await scenario.InvoiceAsync(order.InvoiceId)).ClientId;
        var linked = await scenario.SignInLinkedUserAsync("save-noperm-pay", clientId, ClientPermission.ViewEmails);

        var response = await StartOrderPaymentAsync(linked, scenario, order, savePaymentMethod: false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.Instructions.Should().ContainSingle().Which.SavePaymentMethod.Should().BeFalse();
        (await scenario.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>A user linked to the account with the permission to view and pay invoices keeps the method for the account, the consent recorded for the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Start_WithSaveByALinkedUserWithViewPayInvoices_RecordsTheConsentForTheAccount()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var order = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var clientId = (await scenario.InvoiceAsync(order.InvoiceId)).ClientId;
        var linked = await scenario.SignInLinkedUserAsync("save-withperm", clientId, ClientPermission.ViewPayInvoices);

        var response = await StartOrderPaymentAsync(linked, scenario, order, savePaymentMethod: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.Instructions.Should().ContainSingle().Which.SavePaymentMethod.Should().BeTrue();
        (await scenario.ConsentsOfAsync(clientId)).Should().ContainSingle();
    }
}
