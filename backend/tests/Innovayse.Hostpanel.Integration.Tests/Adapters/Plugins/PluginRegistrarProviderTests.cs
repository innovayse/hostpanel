using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginRegistrarProvider"/>, the one adapter that calls a registrar plugin: each port operation reaches the plugin
/// with the connection, the domain's reference and the mapped details, and each answer comes back as the host's result, with a
/// refusal worded by its code and never by the plugin's text.
/// </summary>
public sealed class PluginRegistrarProviderTests
{
    /// <summary>The connection every test hands the provider.</summary>
    private static readonly RegistrarConnection Connection = new("account", "key", "https://api.example.test", "10.0.0.1", true);

    /// <summary>The domain every test addresses.</summary>
    private static readonly DomainRef Domain = new("example.test", "REF-EXAMPLE");

    /// <summary>The registrant contact every order carries.</summary>
    private static readonly DomainContact Contact =
        new("Grace", "Hopper", "Navy", "grace@example.test", "+1555", "2 Side St", "Floor 3", "City", "ST", "00200", "US");

    /// <summary>Builds the provider over a scripted plugin, declaring the given capabilities in its manifest.</summary>
    /// <param name="plugin">The scripted plugin.</param>
    /// <param name="capabilities">The capabilities the manifest declares.</param>
    /// <returns>The provider under test.</returns>
    private static PluginRegistrarProvider Provider(ScriptedRegistrarPlugin plugin, params string[] capabilities)
    {
        var manifest = PluginCapabilitySeeds.Plugin("scripted", PluginType.Registrar, plugin.GetType(), capabilities).Manifest;

        return new PluginRegistrarProvider(manifest, plugin, Connection, NullLogger<PluginRegistrarProvider>.Instance);
    }

    /// <summary>Builds a full plugin that declares every capability the host calls.</summary>
    /// <returns>The plugin and the provider over it.</returns>
    private static (FullRegistrarPlugin Plugin, PluginRegistrarProvider Provider) Full()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build());

        return (plugin, Provider(
            plugin,
            PluginCapabilities.RegistrarOperations,
            PluginCapabilities.RegistrarDnsManagement,
            PluginCapabilities.RegistrarEmailForwarding,
            PluginCapabilities.RegistrarWhois));
    }

    /// <summary>A registration reaches the plugin with the connection, both nameservers and the mapped contact, and returns the order.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_PassesTheOrderAndReturnsTheRegistrarsReferenceAsync()
    {
        var (plugin, provider) = Full();

        var result = await provider.RegisterAsync(
            new DomainRegistrationOrder("example.test", 2, true, false, "ns1.example.test", "ns2.example.test", Contact), CancellationToken.None);

        result.Should().Be(new RegistrarResult(true, "REF-1", ScriptedRegistrarPlugin.OrderExpiry, null));
        var context = plugin.Received.Should().ContainSingle().Which.Should().BeOfType<RegistrarRegisterContext>().Subject;
        context.Connection.Should().Be(Connection);
        context.DomainName.Should().Be("example.test");
        context.Years.Should().Be(2);
        context.WhoisPrivacy.Should().BeTrue();
        context.AutoRenew.Should().BeFalse();
        context.Nameservers.Should().Equal("ns1.example.test", "ns2.example.test");
        context.Contact.Should().Be(RegistrarContactMapper.ToPlugin(Contact));
    }

    /// <summary>A blank nameserver is not sent, so the plugin never receives an empty entry.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_LeavesOutABlankNameserverAsync()
    {
        var (plugin, provider) = Full();

        await provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, "ns1.example.test", " ", Contact), CancellationToken.None);

        plugin.Received.OfType<RegistrarRegisterContext>().Single().Nameservers.Should().Equal("ns1.example.test");
    }

    /// <summary>An order the plugin accepted but has not finished is a success that asks to be polled.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_PendingOrder_SucceedsAndAsksToBePolledAsync()
    {
        var (plugin, provider) = Full();
        plugin.Order = new ValueResult<RegistrarOrder, RegistrarRefusalReason>(
            PanelOperationOutcome.Pending, new RegistrarOrder("REF-P", null, false), null);

        var result = await provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact), CancellationToken.None);

        result.Should().Be(new RegistrarResult(true, "REF-P", null, null, RequiresPolling: true));
    }

    /// <summary>A refusal comes back as a failed result carrying the host's code for the reason.</summary>
    /// <param name="reason">The plugin's reason.</param>
    /// <param name="code">The host's code for it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(RegistrarRefusalReason.DomainUnavailable, HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE)]
    [InlineData(RegistrarRefusalReason.ContactInvalid, HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED)]
    [InlineData(RegistrarRefusalReason.InsufficientFunds, HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS)]
    [InlineData(RegistrarRefusalReason.Other, HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED)]
    public async Task Register_Refused_ReturnsTheCodeOfTheReasonAsync(RegistrarRefusalReason reason, string code)
    {
        var (plugin, provider) = Full();
        plugin.Order = ValueResult<RegistrarOrder, RegistrarRefusalReason>.Refused(reason);

        var result = await provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(code);
        result.RegistrarRef.Should().BeNull();
    }

    /// <summary>An answer that claims success but carries no order fails closed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_SuccessWithoutAnOrder_FailsClosedAsync()
    {
        var (plugin, provider) = Full();
        plugin.Order = new ValueResult<RegistrarOrder, RegistrarRefusalReason>(PanelOperationOutcome.Succeeded, null, null);

        var result = await provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact), CancellationToken.None);

        result.Should().Be(new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED));
    }

    /// <summary>A plugin that throws is not caught: an exception means the outcome is unknown.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Register_PluginThrows_PropagatesTheExceptionAsync()
    {
        var (plugin, provider) = Full();
        plugin.Throws = new TimeoutException();

        var act = () => provider.RegisterAsync(new DomainRegistrationOrder("example.test", 1, false, false, null, null, Contact), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    /// <summary>An incoming transfer carries the EPP code and the contact.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Transfer_PassesTheEppCodeAndTheContactAsync()
    {
        var (plugin, provider) = Full();

        var result = await provider.TransferAsync(new DomainTransferOrder("example.test", "SECRET-EPP", true, Contact), CancellationToken.None);

        result.Success.Should().BeTrue();
        var context = plugin.Received.OfType<RegistrarTransferContext>().Single();
        context.EppCode.Should().Be("SECRET-EPP");
        context.WhoisPrivacy.Should().BeTrue();
        context.Contact.Should().Be(RegistrarContactMapper.ToPlugin(Contact));
    }

    /// <summary>A renewal carries the registrar's reference and the expiry the host held, so a repeated renewal is recognised.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Renew_PassesTheReferenceYearsAndCurrentExpiryAsync()
    {
        var (plugin, provider) = Full();
        var expires = new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var result = await provider.RenewAsync(new DomainRenewalOrder("example.test", "REF-EXAMPLE", 3, expires), CancellationToken.None);

        result.ExpiresAt.Should().Be(ScriptedRegistrarPlugin.OrderExpiry);
        var context = plugin.Received.OfType<RegistrarRenewContext>().Single();
        context.Domain.Should().Be(new RegistrarDomain("example.test", "REF-EXAMPLE"));
        context.Years.Should().Be(3);
        context.CurrentExpiresAt.Should().Be(expires);
    }

    /// <summary>A domain the plugin reports as pending, or as an order that still requires polling, is not active yet, and one it reports as done is.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CheckDomainActive_PendingOrPollingIsNotActiveAndDoneIsActiveAsync()
    {
        var (plugin, provider) = Full();

        (await provider.CheckDomainActiveAsync(Domain, CancellationToken.None)).Success.Should().BeTrue();

        plugin.Order = new ValueResult<RegistrarOrder, RegistrarRefusalReason>(PanelOperationOutcome.Pending, null, null);
        var pending = await provider.CheckDomainActiveAsync(Domain, CancellationToken.None);

        pending.Should().Be(new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_ACTIVE));

        plugin.Order = ValueResult<RegistrarOrder, RegistrarRefusalReason>.Done(new RegistrarOrder("REF-1", null, true));
        var polling = await provider.CheckDomainActiveAsync(Domain, CancellationToken.None);

        polling.Should().Be(
            new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_ACTIVE),
            "a succeeded order that still requires polling is not live, so the sync must not activate the domain");
    }

    /// <summary>Each flag change reaches the plugin with the domain's reference and the switch.</summary>
    /// <param name="operation">Which flag operation to run.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("autoRenew")]
    [InlineData("privacy")]
    [InlineData("lock")]
    public async Task FlagChanges_PassTheDomainAndTheSwitchAsync(string operation)
    {
        var (plugin, provider) = Full();
        var change = new RegistrarFlagChange(Domain, true);

        var result = operation switch
        {
            "autoRenew" => await provider.SetAutoRenewAsync(change, CancellationToken.None),
            "privacy" => await provider.SetWhoisPrivacyAsync(change, CancellationToken.None),
            _ => await provider.SetRegistrarLockAsync(change, CancellationToken.None),
        };

        result.Success.Should().BeTrue();
        plugin.Received.OfType<RegistrarFlagContext>().Single().Should()
            .Be(new RegistrarFlagContext(Connection, new RegistrarDomain("example.test", "REF-EXAMPLE"), true));
    }

    /// <summary>Cancelling a transfer, starting an outgoing one and changing nameservers reach the plugin with the domain.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TransferControlsAndNameservers_ReachThePluginAsync()
    {
        var (plugin, provider) = Full();

        (await provider.CancelTransferAsync(Domain, CancellationToken.None)).Success.Should().BeTrue();
        (await provider.InitiateOutgoingTransferAsync(Domain, CancellationToken.None)).Success.Should().BeTrue();
        (await provider.SetNameserversAsync(new NameserverChange(Domain, ["ns1.example.test", "ns2.example.test"]), CancellationToken.None))
            .Success.Should().BeTrue();

        plugin.Received.OfType<RegistrarDomainContext>().Should().HaveCount(2);
        plugin.Received.OfType<RegistrarNameserversContext>().Single().Nameservers.Should().Equal("ns1.example.test", "ns2.example.test");
    }

    /// <summary>The EPP code is read from the plugin's answer, and a refusal gives no code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetEppCode_ReturnsTheCodeAndNullOnRefusalAsync()
    {
        var (plugin, provider) = Full();

        (await provider.GetEppCodeAsync(Domain, CancellationToken.None)).Should().Be("EPP-CODE");

        plugin.Epp = ValueResult<RegistrarEppCode, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.TransferNotAllowed);
        (await provider.GetEppCodeAsync(Domain, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>The contact is mapped both ways: read from the plugin and sent to it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Contact_IsMappedBothWaysAsync()
    {
        var (plugin, provider) = Full();

        var read = await provider.GetContactDetailsAsync(Domain, CancellationToken.None);
        var write = await provider.ModifyContactDetailsAsync(new DomainContactChange(Domain, Contact), CancellationToken.None);

        read.Should().NotBeNull();
        read!.FirstName.Should().Be("Ada");
        write.Success.Should().BeTrue();
        plugin.Received.OfType<RegistrarContactContext>().Single().Contact.Should().Be(RegistrarContactMapper.ToPlugin(Contact));
    }

    /// <summary>A DNS update carries the record as it stood and as it must be, a delete carries the record, and an add the new one.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DnsRecords_AddUpdateAndDeleteCarryTheRecordContentAsync()
    {
        var (plugin, provider) = Full();
        var before = DnsRecord.CreateDetached(DnsRecordType.MX, "mail", "old.example.test", 300, 10);
        var after = DnsRecord.CreateDetached(DnsRecordType.MX, "mail", "new.example.test", 600, 20);

        (await provider.AddDnsRecordAsync(new DnsRecordChange(Domain, after), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.UpdateDnsRecordAsync(new DnsRecordUpdate(Domain, before, after), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.DeleteDnsRecordAsync(new DnsRecordRemoval(Domain, before), CancellationToken.None)).Success.Should().BeTrue();

        plugin.Received.OfType<RegistrarDnsRecordContext>().Single().Record
            .Should().Be(new RegistrarDnsRecord(null, RegistrarDnsRecordType.Mx, "mail", "new.example.test", 600, 20));
        var update = plugin.Received.OfType<RegistrarDnsRecordUpdateContext>().Single();
        update.Current.Value.Should().Be("old.example.test");
        update.Updated.Value.Should().Be("new.example.test");
        plugin.Received.OfType<RegistrarDnsRecordRemovalContext>().Single().Record.Value.Should().Be("old.example.test");
    }

    /// <summary>A zone read drops a record type the host cannot hold and keeps the rest.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetDnsRecords_DropsARecordTheHostCannotHoldAsync()
    {
        var (plugin, provider) = Full();
        plugin.DnsRecords = ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason>.Done(
        [
            new RegistrarDnsRecord(null, RegistrarDnsRecordType.Txt, "@", "v=spf1", 300, null),
            new RegistrarDnsRecord(null, RegistrarDnsRecordType.Caa, "@", "0 issue ca.example.test", 300, null),
        ]);

        var records = await provider.GetDnsRecordsAsync(Domain, CancellationToken.None);

        records.Value.Should().ContainSingle().Which.Type.Should().Be(DnsRecordType.TXT);
    }

    /// <summary>A zone the plugin refuses to read is a failure carrying the mapped code, while a domain it does not know has no records.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetDnsRecords_RefusedFailsWithTheMappedCodeAndNotFoundIsEmptyAsync()
    {
        var (plugin, provider) = Full();

        plugin.DnsRecords = ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason>.NotFound();
        (await provider.GetDnsRecordsAsync(Domain, CancellationToken.None)).Value.Should().BeEmpty();

        plugin.DnsRecords = ValueResult<IReadOnlyList<RegistrarDnsRecord>, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.AccessDenied);
        var refused = await provider.GetDnsRecordsAsync(Domain, CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ACCESS_DENIED);
    }

    /// <summary>A name the plugin says is free is available; a taken one, a premium one and a name with a premium price are not.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CheckAvailability_ReportsPremiumNamesAsNotAvailableAsync()
    {
        var (plugin, provider) = Full();

        (await provider.CheckAvailabilityAsync("example.test", CancellationToken.None)).Value.Should().BeTrue();

        plugin.Availability = ValueResult<RegistrarAvailability, RegistrarRefusalReason>.Done(new RegistrarAvailability(false));
        (await provider.CheckAvailabilityAsync("example.test", CancellationToken.None)).Value.Should().BeFalse();

        plugin.Availability = ValueResult<RegistrarAvailability, RegistrarRefusalReason>.Done(
            new RegistrarAvailability(true) { PremiumPrice = new RegistrarPremiumPrice("USD", 5000m, null) });
        (await provider.CheckAvailabilityAsync("example.test", CancellationToken.None)).Value.Should().BeFalse();

        plugin.Availability = ValueResult<RegistrarAvailability, RegistrarRefusalReason>.Done(
            new RegistrarAvailability(true) { Reason = RegistrarAvailabilityReason.Premium });
        (await provider.CheckAvailabilityAsync("example.test", CancellationToken.None)).Value.Should().BeFalse();
    }

    /// <summary>A plugin that does not answer the availability check leaves the outcome unknown: a failure with the mapped code, never read as taken.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CheckAvailability_RefusedFailsWithTheMappedCodeAsync()
    {
        var (plugin, provider) = Full();
        plugin.Availability = ValueResult<RegistrarAvailability, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.TldNotSupported);

        var refused = await provider.CheckAvailabilityAsync("example.test", CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_TLD_NOT_SUPPORTED);
    }

    /// <summary>The price list is mapped with the connection passed on, an unsupported list is empty and a refused one is a failure with the mapped code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task GetTldPricing_MapsEmptyOnNotSupportedAndFailsOnRefusalAsync()
    {
        var (plugin, provider) = Full();

        var pricing = (await provider.GetTldPricingAsync(CancellationToken.None)).Value!;

        var entry = pricing.Should().ContainSingle().Subject;
        entry.Tld.Should().Be("account", "the scripted plugin echoes the connection's account name");
        entry.Currency.Should().Be("key", "the scripted plugin echoes the connection's key");
        entry.Register.Should().Equal(new Dictionary<int, decimal> { [1] = 1m });
        entry.Transfer.Should().Equal(new Dictionary<int, decimal> { [1] = 2m });
        entry.Renew.Should().Equal(new Dictionary<int, decimal> { [1] = 3m });

        plugin.Pricing = ValueResult<IReadOnlyList<RegistrarTldPricing>, RegistrarRefusalReason>.NotSupported();
        (await provider.GetTldPricingAsync(CancellationToken.None)).Value.Should().BeEmpty();

        plugin.Pricing = ValueResult<IReadOnlyList<RegistrarTldPricing>, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.Other);
        var refused = await provider.GetTldPricingAsync(CancellationToken.None);

        refused.IsFailure.Should().BeTrue();
        refused.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>A declared optional capability is called: DNS management, the four email forwarding operations and WHOIS.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OptionalCapabilities_AreCalledWhenDeclaredAsync()
    {
        var (plugin, provider) = Full();

        (await provider.SetDnsManagementAsync(new DomainFeatureToggle(Domain, true), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.SetEmailForwardingAsync(new DomainFeatureToggle(Domain, true), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.AddEmailForwardingRuleAsync(new EmailForwardingRuleChange(Domain, "info", "me@example.test"), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.UpdateEmailForwardingRuleAsync(new EmailForwardingRuleChange(Domain, "info", "you@example.test"), CancellationToken.None)).Success.Should().BeTrue();
        (await provider.DeleteEmailForwardingRuleAsync(new EmailForwardingRuleRemoval(Domain, "info"), CancellationToken.None)).Success.Should().BeTrue();
        var whois = await provider.GetWhoisAsync("example.test", CancellationToken.None);

        whois.Should().NotBeNull();
        whois!.Registrar.Should().Be("Test Registrar");
        plugin.Received.OfType<RegistrarFlagContext>().Should().HaveCount(2);
        plugin.Received.OfType<RegistrarEmailForwardingRuleContext>().Select(c => c.Rule.Destination)
            .Should().Equal("me@example.test", "you@example.test");
        plugin.Received.OfType<RegistrarEmailForwardingRemovalContext>().Single().Source.Should().Be("info");
    }

    /// <summary>A plugin that offers only the operations answers <c>REGISTRAR_OPERATION_UNSUPPORTED</c> for every optional operation and is never asked.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OptionalCapabilities_OperationsOnlyPlugin_AnswersUnsupportedAsync()
    {
        var plugin = new ScriptedRegistrarPlugin(new ConfigurationBuilder().Build());
        var provider = Provider(plugin, PluginCapabilities.RegistrarOperations);
        var unsupported = new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);

        (await provider.SetDnsManagementAsync(new DomainFeatureToggle(Domain, true), CancellationToken.None)).Should().Be(unsupported);
        (await provider.SetEmailForwardingAsync(new DomainFeatureToggle(Domain, true), CancellationToken.None)).Should().Be(unsupported);
        (await provider.AddEmailForwardingRuleAsync(new EmailForwardingRuleChange(Domain, "a", "b@example.test"), CancellationToken.None)).Should().Be(unsupported);
        (await provider.UpdateEmailForwardingRuleAsync(new EmailForwardingRuleChange(Domain, "a", "b@example.test"), CancellationToken.None)).Should().Be(unsupported);
        (await provider.DeleteEmailForwardingRuleAsync(new EmailForwardingRuleRemoval(Domain, "a"), CancellationToken.None)).Should().Be(unsupported);
        (await provider.GetWhoisAsync("example.test", CancellationToken.None)).Should().BeNull();
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A capability the manifest does not declare is not called even when the class implements the interface.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OptionalCapabilities_ImplementedButNotDeclared_AreNotCalledAsync()
    {
        var plugin = new FullRegistrarPlugin(new ConfigurationBuilder().Build());
        var provider = Provider(plugin, PluginCapabilities.RegistrarOperations);

        var result = await provider.SetDnsManagementAsync(new DomainFeatureToggle(Domain, true), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
        plugin.Received.Should().BeEmpty();
    }
}
