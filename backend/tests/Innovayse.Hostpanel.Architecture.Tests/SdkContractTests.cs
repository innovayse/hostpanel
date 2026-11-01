using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Hosting;
using Innovayse.SDK.Plugins.Identity;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Pins the plugin SDK's public surface. A third party compiles against the published package,
/// so a renamed member here is a breaking change for every plugin in the wild; these tests make
/// that a red build rather than a discovery at the consumer's end.
/// </summary>
public sealed class SdkContractTests
{
    /// <summary>The three plugin types P1 adds exist beside the original three and keep their order.</summary>
    [Fact]
    public void PluginType_CarriesTheSixKinds()
    {
        Enum.GetNames<PluginType>().Should().Equal(
            "Provisioning", "Payment", "Registrar", "Identity", "Fulfilment", "Hosted");
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
        typeof(CredentialCustomerDetails).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
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
}
