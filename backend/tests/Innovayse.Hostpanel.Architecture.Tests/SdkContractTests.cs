using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Base;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Host;
using Innovayse.SDK.Plugins.Hosting;
using Innovayse.SDK.Plugins.HostingPanel;
using Innovayse.SDK.Plugins.Identity;
using Innovayse.SDK.Plugins.Lifecycle;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Payment;
using Innovayse.SDK.Plugins.Provisioning;
using Innovayse.SDK.Plugins.Registrar;
using Innovayse.SDK.Plugins.Slots;
using Innovayse.SDK.Plugins.Store;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Pins the plugin SDK's public surface. A third party compiles against the published package,
/// so a renamed member here is a breaking change for every plugin in the wild; these tests make
/// that a red build rather than a discovery at the consumer's end.
/// </summary>
public sealed class SdkContractTests
{
    /// <summary>
    /// The plugin types keep their order and their numbers (the first six were never numbered explicitly, so 0 is a
    /// real kind there), and the app type is appended with the explicit value 6.
    /// </summary>
    [Fact]
    public void PluginType_CarriesTheSevenKindsWithStableValues()
    {
        Enum.GetNames<PluginType>().Should().Equal(
            "Provisioning", "Payment", "Registrar", "Identity", "Fulfilment", "Hosted", "App");
        ((int)PluginType.Provisioning).Should().Be(0);
        ((int)PluginType.Payment).Should().Be(1);
        ((int)PluginType.Registrar).Should().Be(2);
        ((int)PluginType.Identity).Should().Be(3);
        ((int)PluginType.Fulfilment).Should().Be(4);
        ((int)PluginType.Hosted).Should().Be(5);
        ((int)PluginType.App).Should().Be(6);
    }

    /// <summary>The endpoint contract answers one call with one reply and carries no web framework type.</summary>
    [Fact]
    public void IPluginEndpoints_AnswersACallWithAReply()
    {
        var method = typeof(IPluginEndpoints).GetMethods().Should().ContainSingle().Subject;

        method.Name.Should().Be("HandleAsync");
        method.ReturnType.Should().Be(typeof(Task<PluginReply>));
        method.GetParameters().Select(p => p.ParameterType).Should().Equal(typeof(PluginCall), typeof(CancellationToken));
    }

    /// <summary>The call, the caller, the reply and the event keep the members plugins compile against; no token is among them.</summary>
    [Fact]
    public void EndpointTypes_KeepTheirShapeAndCarryNoToken()
    {
        typeof(PluginCall).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Area", "Method", "Route", "Language", "Caller", "Host", "Store", "RouteValues", "Query", "Context", "Body", "ContentType");
        typeof(PluginCaller).GetProperties().Select(p => p.Name).Should().Contain(["ClientRef", "UserRef", "Permissions"]);
        typeof(PluginCaller).GetProperties().Select(p => p.Name).Should().NotContain(
            name => name.Contains("Token", StringComparison.OrdinalIgnoreCase) || name.Contains("Claims", StringComparison.OrdinalIgnoreCase));
        typeof(PluginReply).GetMethods().Where(m => m.IsStatic && m.DeclaringType == typeof(PluginReply)).Select(m => m.Name)
            .Should().BeEquivalentTo("Json", "NoContent", "Problem", "Stream");
        typeof(PluginReply).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Kind", "Status", "Body", "Code", "Detail", "Events");
        typeof(PluginEvent).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Data", "Name", "Id");
    }

    /// <summary>The reply and slot enums keep their names and explicit values; 0 is never valid.</summary>
    [Fact]
    public void PluginAppEnums_KeepTheirValuesAndStartAtOne()
    {
        Enum.GetNames<PluginReplyKind>().Should().Equal("Json", "NoContent", "Problem", "Stream");
        Enum.GetValues<PluginReplyKind>().Select(v => (int)v).Should().Equal(1, 2, 3, 4);
        Enum.GetNames<PluginArea>().Should().Equal("Client", "Admin");
        Enum.GetValues<PluginArea>().Select(v => (int)v).Should().Equal(1, 2);
        Enum.GetNames<PluginHealth>().Should().Equal("Healthy", "Degraded", "Unavailable");
        Enum.GetValues<PluginHealth>().Select(v => (int)v).Should().Equal(1, 2, 3);
        Enum.GetNames<PluginServiceStatus>().Should().Equal("Pending", "Active", "Suspended", "Terminated");
        Enum.GetValues<PluginServiceStatus>().Select(v => (int)v).Should().Equal(1, 2, 3, 4);
        Enum.GetNames<PluginInvoiceStatus>().Should().Equal(
            "Draft", "Unpaid", "Paid", "Overdue", "Cancelled", "Refunded", "Collections", "PaymentPending", "PartiallyRefunded");
        Enum.GetValues<PluginInvoiceStatus>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9);
        Enum.IsDefined((PluginReplyKind)0).Should().BeFalse();
        Enum.IsDefined((PluginArea)0).Should().BeFalse();
        Enum.IsDefined((PluginHealth)0).Should().BeFalse();
        Enum.IsDefined((PluginServiceStatus)0).Should().BeFalse();
        Enum.IsDefined((PluginInvoiceStatus)0).Should().BeFalse();
    }

    /// <summary>
    /// The host API is read only and bound to the call's client: every method is a read, and none takes a client,
    /// service or invoice id.
    /// </summary>
    [Fact]
    public void IPluginHostApi_IsReadOnlyAndTakesNoClientId()
    {
        var methods = typeof(IPluginHostApi).GetMethods();

        methods.Select(m => m.Name).Should().BeEquivalentTo("GetProfileAsync", "ListServicesAsync", "ListInvoicesAsync");
        methods.Should().OnlyContain(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(CancellationToken));
        typeof(IPluginHostApi).GetMethod("GetProfileAsync")!.ReturnType.Should().Be(typeof(Task<PluginClientProfile?>));
        typeof(IPluginHostApi).GetMethod("ListServicesAsync")!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<PluginClientService>>));
        typeof(IPluginHostApi).GetMethod("ListInvoicesAsync")!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<PluginClientInvoice>>));
        typeof(PluginClientProfile).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Name", "Email", "Currency", "Language");
        typeof(PluginClientService).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "ProductId", "Status", "Domain", "CreatedAt", "NextRenewalAt");
        typeof(PluginClientInvoice).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "Status", "Total", "Currency", "InvoiceDate", "DueDate", "PaidAt");
    }

    /// <summary>The store has three operations and none takes a client or plugin scope.</summary>
    [Fact]
    public void IPluginStore_HasGetSetRemoveAndNoScopeArgument()
    {
        var methods = typeof(IPluginStore).GetMethods();

        methods.Select(m => m.Name).Should().BeEquivalentTo("GetAsync", "SetAsync", "RemoveAsync");
        methods.SelectMany(m => m.GetParameters()).Select(p => p.Name).Should().OnlyContain(
            name => name == "key" || name == "value" || name == "ct");
    }

    /// <summary>The optional capabilities exist as separate interfaces; the endpoint contract does not inherit them.</summary>
    [Fact]
    public void OptionalAppCapabilities_AreSeparateInterfaces()
    {
        typeof(IPluginStatus).GetMethods().Select(m => m.Name).Should().BeEquivalentTo("GetStatusAsync");
        typeof(IPluginStatus).GetMethod("GetStatusAsync")!.ReturnType.Should().Be(typeof(Task<PluginStatusReport>));
        typeof(IPluginMigrations).GetMethods().Select(m => m.Name).Should().BeEquivalentTo("MigrateAsync");
        typeof(IPluginStatus).IsAssignableFrom(typeof(IPluginEndpoints)).Should().BeFalse();
        typeof(IPluginMigrations).IsAssignableFrom(typeof(IPluginEndpoints)).Should().BeFalse();
    }

    /// <summary>The slot list is pinned: version, ids, areas and the context keys each offers.</summary>
    [Fact]
    public void PluginSlots_ListsTheFourSlotsOfVersion1()
    {
        PluginSlots.Version.Should().Be("1.0");
        PluginSlots.All.Select(s => (s.Id, s.Area, string.Join(',', s.ContextKeys))).Should().Equal(
            ("client.dashboard.cards", PluginArea.Client, string.Empty),
            ("client.service.tabs", PluginArea.Client, "serviceId"),
            ("admin.client.panels", PluginArea.Admin, "clientId"),
            ("admin.service.panels", PluginArea.Admin, "serviceId,clientId"));
        PluginSlots.Find("admin.service.panels").Should().BeSameAs(PluginSlots.All[3]);
        PluginSlots.Find("client.unknown").Should().BeNull();
        PluginSlots.Find(null).Should().BeNull();
        PluginSlotContextKeys.ServiceId.Should().Be("serviceId");
        PluginSlotContextKeys.ClientId.Should().Be("clientId");
    }

    /// <summary>Every slot id the list names is lower case and unique, so a manifest compares it exactly.</summary>
    [Fact]
    public void PluginSlots_IdsAreUniqueLowerCase()
    {
        PluginSlots.All.Select(s => s.Id).Should().OnlyHaveUniqueItems().And.OnlyContain(id => id == id.ToLowerInvariant());
    }

    /// <summary>A reply is built through factories that refuse a status the shape cannot carry.</summary>
    [Fact]
    public void PluginReply_FactoriesSetShapeAndRefuseWrongStatus()
    {
        var json = PluginReply.Json(new { ok = true }, 201);
        json.Kind.Should().Be(PluginReplyKind.Json);
        json.Status.Should().Be(201);
        json.Body.Should().NotBeNull();
        PluginReply.NoContent().Status.Should().Be(204);
        var problem = PluginReply.Problem(404, "invoice_not_found", "No such invoice.");
        problem.Kind.Should().Be(PluginReplyKind.Problem);
        (problem.Status, problem.Code, problem.Detail).Should().Be((404, "invoice_not_found", "No such invoice."));
        PluginReply.Stream(NoEvents()).Kind.Should().Be(PluginReplyKind.Stream);
        FluentActions.Invoking(() => PluginReply.Json(new { }, 204)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PluginReply.Json(new { }, 404)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PluginReply.Problem(200, "x")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PluginReply.Problem(400, " ")).Should().Throw<ArgumentException>();
    }

    /// <summary>A JSON reply carries a body, so only the four statuses that answer a call with one are accepted.</summary>
    /// <param name="status">The status handed to the factory.</param>
    /// <param name="accepted">Whether the factory must accept it.</param>
    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(202, true)]
    [InlineData(203, true)]
    [InlineData(199, false)]
    [InlineData(204, false)]
    [InlineData(205, false)]
    [InlineData(206, false)]
    [InlineData(299, false)]
    [InlineData(300, false)]
    public void PluginReplyJson_AcceptsOnlyTheBodyBearingSuccessStatuses(int status, bool accepted)
    {
        var act = () => PluginReply.Json(new { ok = true }, status);

        if (accepted)
        {
            act.Should().NotThrow().Which.Status.Should().Be(status);
        }
        else
        {
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>
    /// A problem is a 4xx or 5xx status, but 401 and 407 are challenges the host answers itself, so a plugin refuses a
    /// caller with 403.
    /// </summary>
    /// <param name="status">The status handed to the factory.</param>
    /// <param name="accepted">Whether the factory must accept it.</param>
    [Theory]
    [InlineData(399, false)]
    [InlineData(400, true)]
    [InlineData(401, false)]
    [InlineData(403, true)]
    [InlineData(404, true)]
    [InlineData(407, false)]
    [InlineData(409, true)]
    [InlineData(500, true)]
    [InlineData(599, true)]
    [InlineData(600, false)]
    public void PluginReplyProblem_RefusesTheAuthenticationChallenges(int status, bool accepted)
    {
        var act = () => PluginReply.Problem(status, "code");

        if (accepted)
        {
            act.Should().NotThrow().Which.Status.Should().Be(status);
        }
        else
        {
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>An event needs its data, and its name and id become single lines of the stream, so neither may break the line.</summary>
    [Fact]
    public void PluginEvent_ValidatesItsMembers()
    {
        FluentActions.Invoking(() => new PluginEvent(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PluginEvent("d", "a\nb")).Should().Throw<ArgumentException>().WithParameterName("name");
        FluentActions.Invoking(() => new PluginEvent("d", "a\rb")).Should().Throw<ArgumentException>().WithParameterName("name");
        FluentActions.Invoking(() => new PluginEvent("d", null, "1\ndata: forged")).Should().Throw<ArgumentException>().WithParameterName("id");
        FluentActions.Invoking(() => new PluginEvent("d", null, "1\r")).Should().Throw<ArgumentException>().WithParameterName("id");
        FluentActions.Invoking(() => PluginEvent.FromJson(new { }, "x\ny")).Should().Throw<ArgumentException>().WithParameterName("name");

        var multiLine = new PluginEvent("line one\nline two", "progress", "7");
        (multiLine.Data, multiLine.Name, multiLine.Id).Should().Be(("line one\nline two", "progress", "7"));
        var plain = new PluginEvent(string.Empty);
        (plain.Name, plain.Id).Should().Be((null, null));
    }

    /// <summary>A slot reports exactly the context keys it lists, compared exactly, so a host can validate a manifest with it.</summary>
    [Fact]
    public void PluginSlotOffers_MatchesTheListedContextKeysExactly()
    {
        PluginSlots.Find(PluginSlots.AdminServicePanels)!.Offers(PluginSlotContextKeys.ClientId).Should().BeTrue();
        PluginSlots.Find(PluginSlots.AdminServicePanels)!.Offers(PluginSlotContextKeys.ServiceId).Should().BeTrue();
        PluginSlots.Find(PluginSlots.ClientServiceTabs)!.Offers(PluginSlotContextKeys.ServiceId).Should().BeTrue();
        PluginSlots.Find(PluginSlots.ClientServiceTabs)!.Offers(PluginSlotContextKeys.ClientId).Should().BeFalse();
        PluginSlots.Find(PluginSlots.ClientDashboardCards)!.Offers(PluginSlotContextKeys.ServiceId).Should().BeFalse();
        PluginSlots.Find(PluginSlots.AdminClientPanels)!.Offers("clientid").Should().BeFalse();
        PluginSlots.Find(PluginSlots.AdminClientPanels)!.Offers(string.Empty).Should().BeFalse();
    }

    /// <summary>An event built from a value carries its camelCase JSON, and a caller reports the permissions it holds.</summary>
    [Fact]
    public void PluginEventAndCaller_BehaveAsDocumented()
    {
        PluginEvent.FromJson(new { Step = 2 }, "progress").Should().Be(new PluginEvent("{\"step\":2}", "progress"));
        var caller = new PluginCaller { UserRef = "u", Permissions = ["ViewInvoices"] };
        caller.HasPermission("ViewInvoices").Should().BeTrue();
        caller.HasPermission("viewinvoices").Should().BeFalse();
        new PluginCaller { UserRef = "u", Permissions = null! }.Permissions.Should().BeEmpty();
    }

    /// <summary>
    /// An identity plugin answers with the external account the principal acts for and two capabilities.
    /// </summary>
    [Fact]
    public void IClientResolver_ResolvesAPrincipalToClientAccess()
    {
        var method = typeof(IClientResolver).GetMethod(nameof(IClientResolver.ResolveAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<ClientAccess?>));
        method.GetParameters().Select(p => p.ParameterType)
            .Should().Equal(typeof(ClaimsPrincipal), typeof(CancellationToken));
        typeof(ClientAccess).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("Account", "CanPurchase", "CanManagePaymentMethods");
    }

    /// <summary>A fulfilment plugin has exactly the four hooks the host calls, in the host's vocabulary.</summary>
    [Fact]
    public void IFulfilmentPlugin_HasTheFourHooks()
    {
        typeof(IFulfilmentPlugin).GetMethods().Select(m => m.Name).Should().BeEquivalentTo(
            "OnInvoicePaidAsync", "AdviseRefundAsync", "OnInvoiceRefundedAsync", "OnSubscriptionLapsedAsync");
        typeof(IFulfilmentPlugin).GetMethod("AdviseRefundAsync")!.ReturnType
            .Should().Be(typeof(Task<RefundAdvice>));
    }

    /// <summary>Every fulfilment context carries the invoice, the client, the external account and the lines.</summary>
    /// <param name="context">The fulfilment context type under inspection.</param>
    [Theory]
    [InlineData(typeof(InvoicePaidContext))]
    [InlineData(typeof(RefundAdviceContext))]
    [InlineData(typeof(InvoiceRefundedContext))]
    [InlineData(typeof(SubscriptionLapsedContext))]
    public void FulfilmentContexts_ShareTheCommonShape(Type context)
    {
        var names = context.GetProperties().Select(p => p.Name).ToList();

        names.Should().Contain(["InvoiceId", "ClientId", "Account", "Lines"]);
        context.GetProperty("Lines")!.PropertyType.Should().Be(typeof(IReadOnlyList<FulfilmentLine>));
    }

    /// <summary>The stored credential capability has exactly the eight operations of the contract, all asynchronous.</summary>
    [Fact]
    public void IStoredCredentialPlugin_HasTheEightOperations()
    {
        typeof(IStoredCredentialPlugin).GetMethods().Select(m => m.Name).Should().BeEquivalentTo(
            "EnsureCustomerAsync", "BeginSaveMethodAsync", "CompleteSaveMethodAsync", "ChargeAsync",
            "FindChargeAsync", "ListMethodsAsync", "SetDefaultMethodAsync", "RemoveMethodAsync");
        typeof(IStoredCredentialPlugin).GetMethod("ChargeAsync")!.ReturnType.Should().Be(typeof(Task<StoredChargeResult>));
        typeof(IStoredCredentialPlugin).GetMethod("FindChargeAsync")!.ReturnType.Should().Be(typeof(Task<StoredChargeLookup>));
        typeof(IStoredCredentialPlugin).GetMethod("ListMethodsAsync")!.ReturnType.Should().Be(typeof(Task<IReadOnlyList<SavedMethod>>));
        typeof(IStoredCredentialPlugin).GetMethods().Should().OnlyContain(m => m.GetParameters().Last().ParameterType == typeof(CancellationToken));
    }

    /// <summary>The capability is optional: the payment contract does not inherit it and keeps its three operations.</summary>
    [Fact]
    public void IPaymentPlugin_DoesNotInheritTheStoredCredentialCapability()
    {
        typeof(IStoredCredentialPlugin).IsAssignableFrom(typeof(IPaymentPlugin)).Should().BeFalse();
        typeof(IPaymentPlugin).GetMethods().Select(m => m.Name).Should().BeEquivalentTo(
            "CreatePaymentAsync", "GetStatusAsync", "RefundAsync", "AcceptsCurrency", "get_CurrencyCode");
    }

    /// <summary>The records and enums of the capability keep the members the host and the plugins compile against.</summary>
    [Fact]
    public void StoredCredentialRecordsAndEnums_KeepTheirShape()
    {
        Enum.GetNames<StoredChargeOutcome>().Should().Equal("Succeeded", "Declined", "ActionRequired", "Pending");
        ((int)StoredChargeOutcome.Succeeded).Should().Be(1);
        ((int)StoredChargeOutcome.Declined).Should().Be(2);
        ((int)StoredChargeOutcome.ActionRequired).Should().Be(3);
        ((int)StoredChargeOutcome.Pending).Should().Be(4);
        ((int)SaveMethodMode.EmbeddedToken).Should().Be(1);
        ((int)SaveMethodMode.Redirect).Should().Be(2);
        Enum.IsDefined((StoredChargeOutcome)0).Should().BeFalse();
        Enum.IsDefined((SaveMethodMode)0).Should().BeFalse();
        Enum.GetNames<SaveMethodMode>().Should().Equal("EmbeddedToken", "Redirect");
        typeof(StoredChargeInstruction).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "CustomerRef", "MethodRef", "AmountMinor", "Currency", "InvoiceReference", "IdempotencyKey", "ReturnUrl");
        typeof(StoredChargeResult).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Outcome", "TransactionId", "AmountMinor", "Currency", "ActionUrl");
        typeof(StoredChargeLookup).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Found", "Outcome", "TransactionId", "AmountMinor", "Currency", "ActionUrl");
        typeof(SaveMethodContext).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("CustomerRef", "ReturnUrl");
        typeof(SaveMethodSession).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "SessionId", "Mode", "ClientToken", "RedirectUrl");
        typeof(SavedMethod).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "MethodRef", "Brand", "Last4", "ExpiryMonth", "ExpiryYear", "IsDefault");
        typeof(SavedMethodResult).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Saved", "Method");
        typeof(CredentialCustomerContext).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "ClientReference", "Email", "Name");
        typeof(CredentialCustomerResult).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("CustomerRef");
    }

    /// <summary>The manifest's capabilities list is optional, empty by default, and reads from <c>capabilities</c> in the JSON.</summary>
    [Fact]
    public void PluginManifest_CapabilitiesAreOptionalAndNamed()
    {
        PluginCapabilities.StoredCredentials.Should().Be("storedCredentials");
        var without = new PluginManifest
        {
            Id = "p", Name = "P", Version = "1.0.0", Author = "A", Description = "D", Type = PluginType.Payment,
            Category = "Payment", EntryPoint = "P.Plugin", SdkVersion = "1.0",
        };
        without.Capabilities.Should().BeEmpty();

        var parsed = JsonSerializer.Deserialize<PluginManifest>(
            """{"id":"p","name":"P","version":"1.0.0","author":"A","description":"D","type":"Payment","category":"Payment","entryPoint":"P.Plugin","sdkVersion":"1.0","capabilities":["storedCredentials"]}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } });
        parsed!.Capabilities.Should().Equal(PluginCapabilities.StoredCredentials);
    }

    /// <summary>The manifest v2 enums keep their names and explicit values; 0 is never a kind or a method.</summary>
    [Fact]
    public void ManifestV2Enums_KeepTheirValuesAndStartAtOne()
    {
        Enum.GetNames<PluginSettingType>().Should().Equal(
            "Text", "Url", "Select", "Secret", "Number", "Boolean", "Textarea");
        ((int)PluginSettingType.Text).Should().Be(1);
        ((int)PluginSettingType.Url).Should().Be(2);
        ((int)PluginSettingType.Select).Should().Be(3);
        ((int)PluginSettingType.Secret).Should().Be(4);
        ((int)PluginSettingType.Number).Should().Be(5);
        ((int)PluginSettingType.Boolean).Should().Be(6);
        ((int)PluginSettingType.Textarea).Should().Be(7);
        Enum.IsDefined((PluginSettingType)0).Should().BeFalse();
        Enum.GetNames<PluginRouteMethod>().Should().Equal("Get", "Post", "Put", "Patch", "Delete");
        ((int)PluginRouteMethod.Get).Should().Be(1);
        ((int)PluginRouteMethod.Post).Should().Be(2);
        ((int)PluginRouteMethod.Put).Should().Be(3);
        ((int)PluginRouteMethod.Patch).Should().Be(4);
        ((int)PluginRouteMethod.Delete).Should().Be(5);
        Enum.IsDefined((PluginRouteMethod)0).Should().BeFalse();
        Enum.GetNames<PluginRateLimitPolicy>().Should().Equal("Default", "Strict", "Heavy");
        ((int)PluginRateLimitPolicy.Default).Should().Be(1);
        ((int)PluginRateLimitPolicy.Strict).Should().Be(2);
        ((int)PluginRateLimitPolicy.Heavy).Should().Be(3);
        Enum.IsDefined((PluginRateLimitPolicy)0).Should().BeFalse();
    }

    /// <summary>The manifest v2 types keep the members the host and the plugins compile against.</summary>
    [Fact]
    public void ManifestV2Types_KeepTheirShape()
    {
        typeof(PluginManifest).GetProperties().Select(p => p.Name).Should().Contain(
            ["ManifestVersion", "Locales", "Settings", "Api", "Ui", "ExternalOrigins", "Fields"]);
        typeof(PluginManifest).GetMethod(nameof(PluginManifest.GetSettings))!.ReturnType
            .Should().Be(typeof(IReadOnlyList<PluginSetting>));
        typeof(PluginSetting).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Key", "Label", "Type", "Required", "Options", "Description", "Default", "Placeholder", "Min", "Max");
        typeof(PluginSettingOption).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Value", "Label");
        typeof(PluginApi).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Client", "Admin");
        typeof(PluginRoute).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Method", "Path", "Permission", "RateLimit", "Streaming");
        typeof(PluginUi).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Client", "Admin", "Slots");
        typeof(PluginUiArea).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Menus", "Pages");
        typeof(PluginMenuItem).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Label", "Page", "Icon", "Order");
        typeof(PluginPage).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "Path", "Title", "View", "Permission");
        typeof(PluginSlotEntry).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "Slot", "View", "Title", "Order", "Permission", "ProductIds", "ContextKeys");
    }

    /// <summary>A manifest built in code, as a version 1 plugin author would, is version 1 with every new block empty.</summary>
    [Fact]
    public void PluginManifest_WithoutManifestVersion_IsVersion1WithNothingAdded()
    {
        var manifest = new PluginManifest
        {
            Id = "p", Name = "P", Version = "1.0.0", Author = "A", Description = "D", Type = PluginType.Payment,
            Category = "Payment", EntryPoint = "P.Plugin", SdkVersion = "1.0",
        };

        manifest.ManifestVersion.Should().Be(1);
        manifest.Settings.Should().BeEmpty();
        manifest.Locales.Should().BeEmpty();
        manifest.Api.Client.Should().BeEmpty();
        manifest.Api.Admin.Should().BeEmpty();
        manifest.Ui.Client.Pages.Should().BeEmpty();
        manifest.Ui.Admin.Menus.Should().BeEmpty();
        manifest.Ui.Slots.Should().BeEmpty();
        manifest.ExternalOrigins.Should().BeEmpty();
        manifest.GetSettings().Should().BeEmpty();
    }

    /// <summary>A hosted plugin is started and stopped by the host and nothing else.</summary>
    [Fact]
    public void IHostedPlugin_HasStartAndStop()
    {
        typeof(IHostedPlugin).GetMethods().Select(m => m.Name)
            .Should().BeEquivalentTo("StartAsync", "StopAsync");
    }

    /// <summary>No interface of the SDK sits in an <c>Interfaces</c> namespace: each is beside the records of its area.</summary>
    [Fact]
    public void SdkInterfaces_AreNotInAnInterfacesNamespace()
    {
        var interfaces = Layers.AuthoredTypesOf(Layers.Sdk).Where(type => type.IsInterface && !type.IsNested).ToList();

        interfaces.Should().HaveCountGreaterThan(10);
        interfaces.Where(type => type.Namespace is null || type.Namespace.Split('.').Contains("Interfaces"))
            .Select(type => type.FullName).Should().BeEmpty("interfaces live in their area folder, never in Interfaces/");
    }

    /// <summary>No type sits directly in <c>Innovayse.SDK.Plugins</c>: each belongs to an area folder.</summary>
    [Fact]
    public void SdkPlugins_HasNoTypeAtTheRootNamespace()
    {
        Layers.AuthoredTypesOf(Layers.Sdk).Where(type => type.Namespace == "Innovayse.SDK.Plugins")
            .Select(type => type.FullName).Should().BeEmpty("group each type in an area folder");
    }

    /// <summary>The 2.0.0 move: each released type is in its new namespace.</summary>
    /// <param name="type">The type under inspection.</param>
    /// <param name="expected">The namespace it must be in.</param>
    [Theory]
    [InlineData(typeof(IPaymentPlugin), "Innovayse.SDK.Plugins.Payment")]
    [InlineData(typeof(IProvisioningPlugin), "Innovayse.SDK.Plugins.Provisioning")]
    [InlineData(typeof(IRegistrarPlugin), "Innovayse.SDK.Plugins.Registrar")]
    [InlineData(typeof(IHostingPanelApiClient), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(IHostingPanelV7ApiClient), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(IFulfilmentPlugin), "Innovayse.SDK.Plugins.Fulfilment")]
    [InlineData(typeof(IClientResolver), "Innovayse.SDK.Plugins.Identity")]
    [InlineData(typeof(IHostedPlugin), "Innovayse.SDK.Plugins.Hosting")]
    [InlineData(typeof(IStoredCredentialPlugin), "Innovayse.SDK.Plugins.StoredCredentials")]
    [InlineData(typeof(IPluginEndpoints), "Innovayse.SDK.Plugins.Endpoints")]
    [InlineData(typeof(IPluginHostApi), "Innovayse.SDK.Plugins.Host")]
    [InlineData(typeof(IPluginStore), "Innovayse.SDK.Plugins.Store")]
    [InlineData(typeof(IPluginStatus), "Innovayse.SDK.Plugins.Lifecycle")]
    [InlineData(typeof(IPluginMigrations), "Innovayse.SDK.Plugins.Lifecycle")]
    [InlineData(typeof(PaymentInstruction), "Innovayse.SDK.Plugins.Payment")]
    [InlineData(typeof(PaymentSession), "Innovayse.SDK.Plugins.Payment")]
    [InlineData(typeof(GatewayPaymentStatus), "Innovayse.SDK.Plugins.Payment")]
    [InlineData(typeof(HostingPanelAddress), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(HostingPanelApiReader), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(HostingPanelApiWire), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(HostingPanelConnection), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(HostingPanelServerInfo), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(HostingPanelV7ServerInfo), "Innovayse.SDK.Plugins.HostingPanel")]
    [InlineData(typeof(CredentialCustomerContext), "Innovayse.SDK.Plugins.StoredCredentials")]
    [InlineData(typeof(PluginManifest), "Innovayse.SDK.Plugins.Manifest")]
    [InlineData(typeof(PluginField), "Innovayse.SDK.Plugins.Manifest")]
    [InlineData(typeof(PluginCapabilities), "Innovayse.SDK.Plugins.Manifest")]
    public void ReleasedTypes_AreInTheirAreaNamespace(Type type, string expected)
    {
        type.Namespace.Should().Be(expected);
    }

    /// <summary>
    /// The payment, slot, lifecycle and hosting panel records keep the members plugins compile against, so a
    /// rename of a positional record member is a red build.
    /// </summary>
    [Fact]
    public void PaymentSlotLifecycleAndPanelRecords_KeepTheirMembers()
    {
        typeof(PaymentInstruction).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "OrderNumber", "AmountMinor", "ReturnUrl", "Description", "Language");
        typeof(PaymentSession).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("GatewayOrderId", "RedirectUrl");
        typeof(GatewayPaymentStatus).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "State", "TransactionId", "RawStatus", "AmountMinor", "Currency");
        typeof(PluginSlot).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Area", "ContextKeys");
        typeof(PluginStatusReport).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Health", "Detail");
        typeof(HostingPanelConnection).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Host", "Port", "ApiKey", "BaseUrl");
        typeof(HostingPanelServerInfo).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("AccountsCount", "Version");
        typeof(HostingPanelV7ServerInfo).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("PackagesCount");
        typeof(FulfilmentLine).GetProperty("BillingCycle")!.PropertyType.Should().Be(typeof(PluginBillingCycle?));
    }

    /// <summary>
    /// <see cref="GatewayPaymentState"/> was released with implicit numbers, so 0, 1 and 2 are a contract a
    /// stored or serialised value depends on and must never be renumbered.
    /// </summary>
    [Fact]
    public void GatewayPaymentState_KeepsItsReleasedValues()
    {
        Enum.GetNames<GatewayPaymentState>().Should().Equal("Pending", "Paid", "Declined");
        ((int)GatewayPaymentState.Pending).Should().Be(0);
        ((int)GatewayPaymentState.Paid).Should().Be(1);
        ((int)GatewayPaymentState.Declined).Should().Be(2);
    }

    /// <summary>The billing cycle a fulfilment line names has explicit values from 1, and 0 is not a cycle.</summary>
    [Fact]
    public void PluginBillingCycle_HasExplicitValuesFromOne()
    {
        Enum.GetNames<PluginBillingCycle>().Should().Equal("Monthly", "Annual", "Quarterly", "SemiAnnual", "Biennial");
        Enum.GetValues<PluginBillingCycle>().Select(v => (int)v).Should().Equal(1, 2, 3, 4, 5);
        Enum.IsDefined((PluginBillingCycle)0).Should().BeFalse();
    }

    /// <summary>The "any signed-in caller" permission marker is one documented constant.</summary>
    [Fact]
    public void PluginPermissions_AnyIsTheDocumentedMarker()
    {
        PluginPermissions.Any.Should().Be("Any");
    }

    /// <summary>A missing or <c>null</c> <c>fields</c> reads as an empty list, like every other list of the manifest.</summary>
    [Fact]
    public void PluginManifest_NullFieldsReadAsEmpty()
    {
        var manifest = new PluginManifest
        {
            Id = "p", Name = "P", Version = "1.0.0", Author = "A", Description = "D", Type = PluginType.Payment,
            Category = "Payment", EntryPoint = "P.Plugin", SdkVersion = "1.0", Fields = null!,
        };

        manifest.Fields.Should().NotBeNull().And.BeEmpty();
        manifest.GetSettings().Should().BeEmpty();
        JsonSerializer.Deserialize<PluginManifest>(
                """{"id":"p","name":"P","version":"1.0.0","author":"A","description":"D","type":"Payment","category":"Payment","entryPoint":"P.Plugin","sdkVersion":"1.0","fields":null}""",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } })!
            .Fields.Should().BeEmpty();
    }

    /// <summary>A panel address is HTTPS on the given port, and an IPv6 literal is bracketed so its colons are not read as the port.</summary>
    /// <param name="host">The host handed to the builder.</param>
    /// <param name="expected">The base URL it must build.</param>
    [Theory]
    [InlineData("panel.example.com", "https://panel.example.com:2304")]
    [InlineData("192.0.2.10", "https://192.0.2.10:2304")]
    [InlineData("2001:db8::1", "https://[2001:db8::1]:2304")]
    [InlineData("[2001:db8::1]", "https://[2001:db8::1]:2304")]
    public void HostingPanelAddress_BracketsAnIpv6Host(string host, string expected)
    {
        HostingPanelAddress.BaseUrl(host, 2304).Should().Be(expected);
        new HostingPanelConnection(host, 2304, "key").BaseUrl.Should().Be(expected);
        new Uri(expected).Port.Should().Be(2304);
    }

    /// <summary>A panel address needs a host: a null, empty or blank one is refused instead of building a URL without a server.</summary>
    [Fact]
    public void HostingPanelAddress_RefusesAMissingHost()
    {
        FluentActions.Invoking(() => HostingPanelAddress.BaseUrl(null!, 2304)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => HostingPanelAddress.BaseUrl(string.Empty, 2304)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => HostingPanelAddress.BaseUrl("  ", 2304)).Should().Throw<ArgumentException>();
    }

    /// <summary>The account envelope is a sealed record with init-only members: nothing can change it after it is read.</summary>
    [Fact]
    public void HostingPanelApiWire_IsAnImmutableRecord()
    {
        typeof(HostingPanelApiWire).IsSealed.Should().BeTrue();
        typeof(HostingPanelApiWire).GetMethod("<Clone>$").Should().NotBeNull("it is a record");
        typeof(HostingPanelApiWire).GetProperties().Where(p => p.SetMethod is not null)
            .Should().OnlyContain(p => p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)));
        HostingPanelApiWire.Failure("x").IsSuccess.Should().BeFalse();
        new HostingPanelApiWire { Status = HostingPanelApiWire.OkStatus }.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A base class refuses a missing id, configuration or logger when it is built and a missing key when it reads, and
    /// reads the setting under <c>integration:{id}:{key}</c>.
    /// </summary>
    [Fact]
    public void ProviderBases_RefuseNullArgumentsAndReadTheirOwnKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["integration:demo:token"] = "abc" })
            .Build();

        var gateway = new GatewayProbe("demo", configuration);
        gateway.Read("token").Should().Be("abc");
        gateway.Read("missing").Should().BeNull();
        FluentActions.Invoking(() => gateway.Read(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gateway.Read(" ")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new GatewayProbe(null!, configuration)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new GatewayProbe(" ", configuration)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new GatewayProbe("demo", null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProvisioningProbe("demo", configuration, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new RegistrarProbe("demo", null!)).Should().Throw<ArgumentNullException>();
        new ProvisioningProbe("demo", configuration, NullLogger.Instance).Read("token").Should().Be("abc");
        new RegistrarProbe("demo", configuration).Read("token").Should().Be("abc");
    }

    /// <summary>A payment base class that exposes what the base reads, so the test can reach the protected members.</summary>
    private sealed class GatewayProbe : PaymentGatewayBase
    {
        /// <summary>Initializes a new instance of the <see cref="GatewayProbe"/> class.</summary>
        /// <param name="pluginId">The plugin id handed to the base.</param>
        /// <param name="configuration">The configuration handed to the base.</param>
        public GatewayProbe(string pluginId, IConfiguration configuration)
            : base(pluginId, configuration, NullLogger.Instance)
        {
        }

        /// <summary>Reads a setting through the base class.</summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        public string? Read(string key) => GetConfig(key);
    }

    /// <summary>A provisioning base class that exposes what the base reads.</summary>
    private sealed class ProvisioningProbe : ProvisioningProviderBase
    {
        /// <summary>Initializes a new instance of the <see cref="ProvisioningProbe"/> class.</summary>
        /// <param name="pluginId">The plugin id handed to the base.</param>
        /// <param name="configuration">The configuration handed to the base.</param>
        /// <param name="logger">The logger handed to the base.</param>
        public ProvisioningProbe(string pluginId, IConfiguration configuration, ILogger logger)
            : base(pluginId, configuration, logger)
        {
        }

        /// <summary>Reads a setting through the base class.</summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        public string? Read(string key) => GetConfig(key);
    }

    /// <summary>A registrar base class that exposes what the base reads.</summary>
    private sealed class RegistrarProbe : RegistrarProviderBase
    {
        /// <summary>Initializes a new instance of the <see cref="RegistrarProbe"/> class.</summary>
        /// <param name="pluginId">The plugin id handed to the base.</param>
        /// <param name="configuration">The configuration handed to the base.</param>
        public RegistrarProbe(string pluginId, IConfiguration configuration)
            : base(pluginId, configuration, NullLogger.Instance)
        {
        }

        /// <summary>Reads a setting through the base class.</summary>
        /// <param name="key">The setting key.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        public string? Read(string key) => GetConfig(key);
    }

    /// <summary>An event sequence that ends at once, for a reply whose events do not matter.</summary>
    /// <returns>The empty sequence.</returns>
    private static async IAsyncEnumerable<PluginEvent> NoEvents()
    {
        await Task.CompletedTask;
        yield break;
    }
}
