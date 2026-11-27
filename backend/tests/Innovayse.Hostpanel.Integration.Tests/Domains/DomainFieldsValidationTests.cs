using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RegisterDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferDomain;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// The extra values a registry asks for are checked against the fields the registrar plugin's manifest declares before an order
/// is placed and before the registrar is called, kept encrypted with the order line and the domain, handed to the registrar
/// unchanged on a registration, a transfer and a contact change, and read back on the contact. Every zone is invented per test:
/// the host takes the zones and the fields from the manifest and holds none of its own.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainFieldsValidationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The password every guest checkout in these tests signs up with.</summary>
    private const string GuestPassword = "Pass@123!";

    /// <summary>
    /// Builds the one set most tests declare for every operation: a required text with a pattern and an optional select.
    /// </summary>
    /// <param name="zone">The invented zone.</param>
    /// <returns>The sets.</returns>
    private static IReadOnlyList<PluginDomainFieldSet> AllOperationsSet(string zone)
    {
        return
        [
            new PluginDomainFieldSet
            {
                Zones = [zone],
                Fields =
                [
                    new PluginDomainField
                    {
                        Key = "holder-number",
                        Label = "Holder number",
                        Type = PluginDomainFieldType.Text,
                        Required = true,
                        Pattern = "[A-Z]{2}[0-9]{4}",
                    },
                    new PluginDomainField
                    {
                        Key = "category",
                        Label = "Category",
                        Type = PluginDomainFieldType.Select,
                        Options = [new PluginSettingOption { Value = "a", Label = "A" }, new PluginSettingOption { Value = "b", Label = "B" }],
                    },
                ],
            },
        ];
    }

    /// <summary>Builds two sets for one zone, one asking a person for a field and the other asking an organisation for another.</summary>
    /// <param name="zone">The invented zone.</param>
    /// <returns>The sets.</returns>
    private static IReadOnlyList<PluginDomainFieldSet> PerHolderSets(string zone)
    {
        return
        [
            new PluginDomainFieldSet
            {
                Zones = [zone],
                Holder = PluginDomainHolder.Individual,
                Fields = [FakeRegistrarDomainFieldCatalogue.Field("personal-number")],
            },
            new PluginDomainFieldSet
            {
                Zones = [zone],
                Holder = PluginDomainHolder.Organization,
                Fields = [FakeRegistrarDomainFieldCatalogue.Field("organization-number")],
            },
        ];
    }

    /// <summary>Builds the registration command the admin's route and order fulfilment both dispatch.</summary>
    /// <param name="clientId">The client the domain is registered for.</param>
    /// <param name="domainName">The domain name.</param>
    /// <param name="attributes">The registry's extra values.</param>
    /// <returns>The command.</returns>
    private static RegisterDomainCommand RegisterCommand(int clientId, string domainName, Dictionary<string, string>? attributes)
    {
        return new RegisterDomainCommand(
            clientId, domainName, 1, WhoisPrivacy: false, AutoRenew: false, Nameserver1: null, Nameserver2: null,
            ExtendedAttributes: attributes);
    }

    /// <summary>Dispatches a command on a scenario's host.</summary>
    /// <typeparam name="T">The command's answer.</typeparam>
    /// <param name="scenario">The scenario whose host answers.</param>
    /// <param name="command">The command.</param>
    /// <returns>The answer.</returns>
    private static async Task<T> InvokeAsync<T>(DomainFieldsScenario scenario, object command)
    {
        using var scope = scenario.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<T>(command);
    }

    /// <summary>Seeds a client the registrar orders are placed for.</summary>
    /// <returns>The client's id.</returns>
    private async Task<int> SeedClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await DomainsTestSeeds.SeedClientAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>A required field with no value refuses the registration with its key, and the registrar is never asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_MissingRequiredField_IsRefusedAndTheRegistrarIsNotCalledAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var name = scenario.NewDomainName();

        var outcome = await InvokeAsync<Result<int>>(scenario, RegisterCommand(await SeedClientAsync(), name, null));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        outcome.Errors.Should().ContainKey("holder-number");
        scenario.Registrar.Registrations.Should().BeEmpty();
    }

    /// <summary>A key no field declares refuses the registration, so a value is never sent to a plugin that did not ask for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_UnknownField_IsRefusedAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);

        var outcome = await InvokeAsync<Result<int>>(
            scenario,
            RegisterCommand(
                await SeedClientAsync(), scenario.NewDomainName(),
                new Dictionary<string, string> { ["holder-number"] = "AB1234", ["undeclared"] = "x" }));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_UNKNOWN);
        outcome.Errors.Should().ContainKey("undeclared");
        scenario.Registrar.Registrations.Should().BeEmpty();
    }

    /// <summary>A value that breaks a field's pattern or options is refused as invalid.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="value">The refused value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("holder-number", "not-a-number")]
    [InlineData("category", "c")]
    public async Task RegisterDomain_ValueTheFieldRefuses_IsRefusedAsInvalidAsync(string key, string value)
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var attributes = new Dictionary<string, string> { ["holder-number"] = "AB1234", [key] = value };

        var outcome = await InvokeAsync<Result<int>>(
            scenario, RegisterCommand(await SeedClientAsync(), scenario.NewDomainName(), attributes));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_INVALID);
        outcome.Errors.Should().ContainKey(key);
        scenario.Registrar.Registrations.Should().BeEmpty();
    }

    /// <summary>Acceptable values reach the registrar unchanged and are stored on the domain, a blank optional one left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_ValidValues_ReachTheRegistrarAndAreStoredAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var name = scenario.NewDomainName();

        var outcome = await InvokeAsync<Result<int>>(
            scenario,
            RegisterCommand(
                await SeedClientAsync(), name,
                new Dictionary<string, string> { ["holder-number"] = "AB1234", ["category"] = string.Empty }));

        outcome.IsSuccess.Should().BeTrue();
        scenario.Registrar.Registrations.Should().ContainSingle().Which.ExtendedAttributes
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "AB1234" });
        using var scope = factory.Services.CreateScope();
        var domain = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Domains
            .AsNoTracking().SingleAsync(d => d.Id == outcome.Value);
        domain.ExtendedAttributes.Values.Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "AB1234" });
    }

    /// <summary>Through the admin's route a refusal is 422 with the code and the field's key in <c>errors</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterRoute_MissingRequiredField_Returns422WithTheKeyAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);

        var response = await scenario.Admin.PostAsJsonAsync("/api/domains/register", new
        {
            domainName = scenario.NewDomainName(),
            years = 1,
            clientId = await SeedClientAsync(),
            whoisPrivacy = false,
            autoRenew = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        body.RootElement.GetProperty("errors").TryGetProperty("holder-number", out _).Should().BeTrue();
    }

    /// <summary>A client with no company is a person, so a zone that asks a person and an organisation different things asks the person's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterDomain_ClientWithoutACompany_IsHeldToTheIndividualsFieldsAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, PerHolderSets);

        var outcome = await InvokeAsync<Result<int>>(
            scenario,
            RegisterCommand(
                await SeedClientAsync(), scenario.NewDomainName(),
                new Dictionary<string, string> { ["organization-number"] = "O-1" }));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        outcome.Errors.Should().ContainKey("personal-number");
    }

    /// <summary>The kind of registrant follows the contact: a named organisation is an organisation, anything else a person.</summary>
    /// <param name="organization">The organisation on the contact.</param>
    /// <param name="expected">The holder it makes.</param>
    [Theory]
    [InlineData(null, PluginDomainHolder.Individual)]
    [InlineData("", PluginDomainHolder.Individual)]
    [InlineData("   ", PluginDomainHolder.Individual)]
    [InlineData("Analytical Engines Ltd", PluginDomainHolder.Organization)]
    public void HolderOf_FollowsTheOrganisationOnTheContact(string? organization, PluginDomainHolder expected)
    {
        DomainFieldsValidation.HolderOf(organization).Should().Be(expected);
    }

    /// <summary>A transfer is checked the same way and the values reach the registrar and the new domain.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferDomain_ChecksTheValuesAndPassesThemOnAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var clientId = await SeedClientAsync();
        var name = scenario.NewDomainName();

        var refused = await InvokeAsync<Result<int>>(
            scenario, new TransferDomainCommand(clientId, name, "auth-code-1", WhoisPrivacy: false));
        var accepted = await InvokeAsync<Result<int>>(
            scenario,
            new TransferDomainCommand(
                clientId, name, "auth-code-1", WhoisPrivacy: false,
                ExtendedAttributes: new Dictionary<string, string> { ["holder-number"] = "CD5678" }));

        refused.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        accepted.IsSuccess.Should().BeTrue();
        scenario.Registrar.Transfers.Should().ContainSingle().Which.ExtendedAttributes
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "CD5678" });
        using var scope = factory.Services.CreateScope();
        var domain = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Domains
            .AsNoTracking().SingleAsync(d => d.Id == accepted.Value);
        domain.ExtendedAttributes.Values.Should().ContainKey("holder-number");
    }

    /// <summary>Seeds a domain held at the scenario's registrar, with a reference so its contact can be changed.</summary>
    /// <param name="scenario">The scenario whose zone the domain is under.</param>
    /// <returns>The domain's id.</returns>
    private async Task<int> SeedHeldDomainAsync(DomainFieldsScenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, scenario.NewDomainName(), TestMoments.Seeded, TestMoments.Seeded.AddYears(1),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base, registrarModule: TestRegistrarModules.First);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-FIELDS", domain.ExpiresAt, TestMoments.Seeded);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain.Id;
    }

    /// <summary>Builds the body of a contact change.</summary>
    /// <param name="attributes">The registry's extra values, or <see langword="null"/> for none.</param>
    /// <returns>The anonymous body.</returns>
    private static object ContactBody(Dictionary<string, string>? attributes)
    {
        // The contact is the stub registrar's own, so no country or address is written here.
        var contact = StubRegistrarProvider.StubContact;
        return new
        {
            firstName = contact.FirstName,
            lastName = contact.LastName,
            organization = (string?)null,
            email = contact.Email,
            phone = contact.Phone,
            address1 = contact.Address1,
            city = contact.City,
            state = contact.State,
            postalCode = contact.PostalCode,
            country = contact.Country,
            extendedAttributes = attributes,
        };
    }

    /// <summary>A contact change with a required value missing is refused with 422 before the registrar is called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyDomainContact_MissingRequiredField_Returns422AndTheRegistrarIsNotCalledAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var id = await SeedHeldDomainAsync(scenario);

        var response = await scenario.Admin.PostAsJsonAsync($"/api/domains/{id}/modify-contact", ContactBody(null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        scenario.Registrar.ContactChanges.Should().BeEmpty();
    }

    /// <summary>
    /// A contact change hands the values to the registrar, replaces the ones stored on the domain, and the contact read gives
    /// them back so the form shows what the registrar holds.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyDomainContact_ValidValues_ReachTheRegistrarReplaceTheStoredOnesAndReadBackAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var id = await SeedHeldDomainAsync(scenario);

        var changed = await scenario.Admin.PostAsJsonAsync(
            $"/api/domains/{id}/modify-contact", ContactBody(new Dictionary<string, string> { ["holder-number"] = "EF9012" }));

        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        scenario.Registrar.ContactChanges.Should().ContainSingle().Which.ExtendedAttributes
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "EF9012" });
        var read = await scenario.Admin.GetFromJsonAsync<JsonElement>($"/api/domains/{id}/contact");
        read.GetProperty("extendedAttributes").GetProperty("holder-number").GetString().Should().Be("EF9012");
    }

    /// <summary>A contact change with no values clears the ones stored, because the registrar now holds none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ModifyDomainContact_NoValuesWhereNoneAreAsked_ClearsTheStoredOnesAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, _ => []);
        var id = await SeedHeldDomainAsync(scenario);

        var changed = await scenario.Admin.PostAsJsonAsync($"/api/domains/{id}/modify-contact", ContactBody(null));

        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var read = await scenario.Admin.GetFromJsonAsync<JsonElement>($"/api/domains/{id}/contact");
        read.GetProperty("extendedAttributes").EnumerateObject().Should().BeEmpty();
    }

    /// <summary>Enables the bank transfer gateway, the one payment method a guest checkout here is placed with.</summary>
    /// <param name="scenario">The scenario whose admin client is used.</param>
    /// <returns>A task that completes when the gateway is enabled.</returns>
    private static async Task EnableBankTransferAsync(DomainFieldsScenario scenario)
    {
        var response = await scenario.Admin.PutAsJsonAsync(
            "/api/admin/integrations/bank-transfer",
            new { isEnabled = true, config = new Dictionary<string, string> { ["iban"] = BankTransferTestSeeds.Iban } });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Builds a guest checkout with one domain registration line.</summary>
    /// <param name="productId">The domain product.</param>
    /// <param name="email">The guest's address.</param>
    /// <param name="domain">The domain to register.</param>
    /// <param name="attributes">The registry's extra values, or <see langword="null"/> for none.</param>
    /// <returns>The anonymous body.</returns>
    private static object GuestOrder(int productId, string email, string domain, Dictionary<string, string>? attributes)
    {
        return new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email,
            password = GuestPassword,
            paymentMethod = "bank_transfer",
            currency = "USD",
            items = new[]
            {
                new
                {
                    productId,
                    billingCycle = nameof(BillingCycle.Annual),
                    domain,
                    domainAction = nameof(DomainAction.Register),
                    years = 1,
                    extendedAttributes = attributes,
                },
            },
        };
    }

    /// <summary>
    /// A checkout whose line misses a required value is refused with 422 before anything is created: no account, no order and no
    /// invoice exist for the payer to pay.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_MissingRequiredField_Returns422AndCreatesNothingAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet, withPrices: true);
        await EnableBankTransferAsync(scenario);
        var productId = await OrdersTestSeeds.SeedDomainProductAsync(factory);
        var email = $"guest-{Guid.NewGuid():N}@example.test";

        var response = await scenario.Host.CreateClient()
            .PostAsJsonAsync("/api/orders", GuestOrder(productId, email, scenario.NewDomainName(), null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    /// <summary>An order whose line carries acceptable values is placed and the values are kept on the line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_ValidValues_AreKeptOnTheOrderLineAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet, withPrices: true);
        await EnableBankTransferAsync(scenario);
        var productId = await OrdersTestSeeds.SeedDomainProductAsync(factory);
        var name = scenario.NewDomainName();

        var response = await scenario.Host.CreateClient().PostAsJsonAsync(
            "/api/orders",
            GuestOrder(productId, $"guest-{Guid.NewGuid():N}@example.test", name, new Dictionary<string, string> { ["holder-number"] = "GH3456" }));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var line = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OrderItems
            .AsNoTracking().SingleAsync(i => i.Domain == name);
        line.ExtendedAttributes.Values.Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "GH3456" });
    }

    /// <summary>Values sent with a line that is no registration or transfer are refused by the validator, not silently dropped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_ValuesOnAHostingLine_Return400Async()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            email = $"guest-{Guid.NewGuid():N}@example.test",
            password = GuestPassword,
            paymentMethod = "bank_transfer",
            items = new[]
            {
                new
                {
                    productId = 1,
                    billingCycle = nameof(BillingCycle.Monthly),
                    extendedAttributes = new Dictionary<string, string> { ["holder-number"] = "AB1234" },
                },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// Fulfilling a paid order's registration line hands the values the line was ordered with to the registrar and stores them on
    /// the new domain, so a retry long after the order was placed sends the same values.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FulfillOrderLines_HandsTheLinesValuesToTheRegistrarAsync()
    {
        var scenario = await DomainFieldsScenario.StartAsync(factory, AllOperationsSet);
        var name = scenario.NewDomainName();
        int orderId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
            var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank_transfer", null, "USD", TestMoments.Seeded);
            order.AddItem(
                productId, name, BillingCycle.Annual, 10m, 10m, domain: name, hostname: null, domainAction: DomainAction.Register,
                years: 1,
                extendedAttributes: DomainExtendedAttributes.From(new Dictionary<string, string> { ["holder-number"] = "IJ7890" }));
            context.Orders.Add(order);
            await context.SaveChangesAsync(CancellationToken.None);
            order.Accept(TestMoments.Seeded);
            await context.SaveChangesAsync(CancellationToken.None);
            orderId = order.Id;
        }

        var fulfilled = await InvokeAsync<Result<OrderFulfillmentDto>>(scenario, new FulfillOrderLinesCommand(orderId));

        fulfilled.IsSuccess.Should().BeTrue();
        fulfilled.Value!.FailedLines.Should().BeEmpty();
        scenario.Registrar.Registrations.Should().ContainSingle().Which.ExtendedAttributes
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["holder-number"] = "IJ7890" });
        using var readScope = factory.Services.CreateScope();
        var domain = await readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Domains
            .AsNoTracking().SingleAsync(d => d.Name == name);
        domain.ExtendedAttributes.Values.Should().ContainKey("holder-number");
    }
}
