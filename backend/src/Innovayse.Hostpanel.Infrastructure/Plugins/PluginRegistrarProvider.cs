using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one adapter that calls a registrar plugin: it turns each <see cref="IRegistrarProvider"/> operation into the SDK's
/// context, calls the plugin, and turns the answer back into a <see cref="RegistrarResult"/>. Nothing else in the host
/// touches <see cref="IRegistrarOperationsPlugin"/>.
/// </summary>
/// <remarks>
/// <para>
/// A refusal is a failed result carrying a <c>REGISTRAR_*</c> code (<see cref="RegistrarRefusalMap"/>); the plugin's own text
/// is never read, so it cannot reach a response. A plugin that throws is not caught: an exception means the outcome is
/// unknown, which is what the port promises.
/// </para>
/// <para>
/// An optional operation (DNS management, email forwarding, WHOIS, the domain list) is reached only when the manifest declares
/// the capability and the plugin implements its interface; otherwise it answers <c>REGISTRAR_OPERATION_UNSUPPORTED</c>. The
/// other optional capabilities a registrar plugin may declare (balance, child nameservers, DNSSEC, restore, suggestions) have
/// no operation in the port yet and are accepted but not called.
/// </para>
/// </remarks>
public sealed class PluginRegistrarProvider : IRegistrarProvider
{
    /// <summary>The id of the registrar plugin, for the log.</summary>
    private readonly string _pluginId;

    /// <summary>The plugin, through the contract every registrar plugin implements.</summary>
    private readonly IRegistrarOperationsPlugin _plugin;

    /// <summary>The plugin as its DNS management contract, or <see langword="null"/> when it does not declare the capability.</summary>
    private readonly IRegistrarDnsManagementPlugin? _dnsManagement;

    /// <summary>The plugin as its email forwarding contract, or <see langword="null"/> when it does not declare the capability.</summary>
    private readonly IRegistrarEmailForwardingPlugin? _emailForwarding;

    /// <summary>The plugin as its WHOIS contract, or <see langword="null"/> when it does not declare the capability.</summary>
    private readonly IRegistrarWhoisPlugin? _whois;

    /// <summary>The plugin as its domain list contract, or <see langword="null"/> when it does not declare the capability.</summary>
    private readonly IRegistrarDomainListPlugin? _domainList;

    /// <summary>The connection built from the plugin's saved settings, handed to the plugin with every call.</summary>
    private readonly RegistrarConnection _connection;

    /// <summary>Records a refusal by plugin id, operation and reason, never by the plugin's own text.</summary>
    private readonly ILogger<PluginRegistrarProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="PluginRegistrarProvider"/> class.</summary>
    /// <param name="manifest">The plugin's manifest, whose declared capabilities decide which optional operations are reached.</param>
    /// <param name="plugin">The activated plugin.</param>
    /// <param name="connection">The connection built from the plugin's saved settings.</param>
    /// <param name="logger">Records a refusal by plugin id, operation and reason.</param>
    public PluginRegistrarProvider(
        PluginManifest manifest,
        IRegistrarOperationsPlugin plugin,
        RegistrarConnection connection,
        ILogger<PluginRegistrarProvider> logger)
    {
        _pluginId = manifest.Id;
        _plugin = plugin;
        _connection = connection;
        _logger = logger;
        _dnsManagement = manifest.Declares(PluginCapabilities.RegistrarDnsManagement) ? plugin as IRegistrarDnsManagementPlugin : null;
        _emailForwarding = manifest.Declares(PluginCapabilities.RegistrarEmailForwarding) ? plugin as IRegistrarEmailForwardingPlugin : null;
        _whois = manifest.Declares(PluginCapabilities.RegistrarWhois) ? plugin as IRegistrarWhoisPlugin : null;
        _domainList = manifest.Declares(PluginCapabilities.RegistrarDomainList) ? plugin as IRegistrarDomainListPlugin : null;
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        List<string> nameservers = [];
        foreach (var nameserver in new[] { request.Nameserver1, request.Nameserver2 })
        {
            if (!string.IsNullOrWhiteSpace(nameserver))
            {
                nameservers.Add(nameserver);
            }
        }

        var answer = await _plugin.RegisterAsync(
            new RegistrarRegisterContext(
                _connection,
                request.DomainName,
                request.Years,
                request.WhoisPrivacy,
                request.AutoRenew,
                nameservers,
                RegistrarContactMapper.ToPlugin(request.Contact)),
            ct);

        return Order(nameof(RegisterAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct)
    {
        var answer = await _plugin.TransferAsync(
            new RegistrarTransferContext(
                _connection,
                request.DomainName,
                request.EppCode,
                request.WhoisPrivacy,
                RegistrarContactMapper.ToPlugin(request.Contact)),
            ct);

        return Order(nameof(TransferAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
    {
        var answer = await _plugin.RenewAsync(
            new RegistrarRenewContext(
                _connection,
                new RegistrarDomain(request.DomainName, request.RegistrarRef),
                request.Years,
                request.CurrentExpiresAt),
            ct);

        return Order(nameof(RenewAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.CancelTransferAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);

        return Settle(nameof(CancelTransferAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.InitiateOutgoingTransferAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);

        return Settle(nameof(InitiateOutgoingTransferAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        var answer = await _plugin.SetAutoRenewAsync(new RegistrarFlagContext(_connection, ToPlugin(change.Domain), change.Enabled), ct);

        return Settle(nameof(SetAutoRenewAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        var answer = await _plugin.SetWhoisPrivacyAsync(new RegistrarFlagContext(_connection, ToPlugin(change.Domain), change.Enabled), ct);

        return Settle(nameof(SetWhoisPrivacyAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        var answer = await _plugin.SetRegistrarLockAsync(new RegistrarFlagContext(_connection, ToPlugin(change.Domain), change.Enabled), ct);

        return Settle(nameof(SetRegistrarLockAsync), answer);
    }

    /// <inheritdoc />
    public async Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.GetEppCodeAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } epp)
        {
            return epp.Code;
        }

        LogRefusal(nameof(GetEppCodeAsync), answer.Outcome, answer.Reason);

        return null;
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct)
    {
        var answer = await _plugin.SetNameserversAsync(
            new RegistrarNameserversContext(_connection, ToPlugin(change.Domain), change.Nameservers), ct);

        return Settle(nameof(SetNameserversAsync), answer);
    }

    /// <inheritdoc />
    /// <remarks>A zone the registrar has no record of, or cannot hold, reads as empty; any other refusal is the failure code.</remarks>
    public async Task<Result<IReadOnlyList<DnsRecord>>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.GetDnsRecordsAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } records)
        {
            return Result<IReadOnlyList<DnsRecord>>.Success(
                records.Select(RegistrarDnsRecordMapper.FromPlugin).OfType<DnsRecord>().ToList());
        }

        if (answer.Outcome is PanelOperationOutcome.NotFound or PanelOperationOutcome.NotSupported)
        {
            return Result<IReadOnlyList<DnsRecord>>.Success([]);
        }

        return ReadFailure<IReadOnlyList<DnsRecord>>(nameof(GetDnsRecordsAsync), answer.Outcome, answer.Reason);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
    {
        var answer = await _plugin.AddDnsRecordAsync(
            new RegistrarDnsRecordContext(_connection, ToPlugin(change.Domain), RegistrarDnsRecordMapper.ToPlugin(change.Record)), ct);

        return Settle(nameof(AddDnsRecordAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordUpdate update, CancellationToken ct)
    {
        var answer = await _plugin.UpdateDnsRecordAsync(
            new RegistrarDnsRecordUpdateContext(
                _connection,
                ToPlugin(update.Domain),
                RegistrarDnsRecordMapper.ToPlugin(update.Current),
                RegistrarDnsRecordMapper.ToPlugin(update.Updated)),
            ct);

        return Settle(nameof(UpdateDnsRecordAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct)
    {
        var answer = await _plugin.DeleteDnsRecordAsync(
            new RegistrarDnsRecordRemovalContext(_connection, ToPlugin(removal.Domain), RegistrarDnsRecordMapper.ToPlugin(removal.Record)), ct);

        return Settle(nameof(DeleteDnsRecordAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct)
    {
        var answer = await _plugin.SetContactAsync(
            new RegistrarContactContext(_connection, ToPlugin(change.Domain), RegistrarContactMapper.ToPlugin(change.Contact)), ct);

        return Settle(nameof(ModifyContactDetailsAsync), answer);
    }

    /// <inheritdoc />
    public async Task<DomainContact?> GetContactDetailsAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.GetContactAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } contact)
        {
            return RegistrarContactMapper.FromPlugin(contact);
        }

        LogRefusal(nameof(GetContactDetailsAsync), answer.Outcome, answer.Reason);

        return null;
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        if (_emailForwarding is null)
        {
            return Unsupported();
        }

        var answer = await _emailForwarding.SetEmailForwardingAsync(
            new RegistrarFlagContext(_connection, ToPlugin(toggle.Domain), toggle.Enabled), ct);

        return Settle(nameof(SetEmailForwardingAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        if (_emailForwarding is null)
        {
            return Unsupported();
        }

        var answer = await _emailForwarding.AddEmailForwardingRuleAsync(
            new RegistrarEmailForwardingRuleContext(
                _connection, ToPlugin(change.Domain), new RegistrarEmailForwardingRule(change.Source, change.Destination)),
            ct);

        return Settle(nameof(AddEmailForwardingRuleAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        if (_emailForwarding is null)
        {
            return Unsupported();
        }

        var answer = await _emailForwarding.UpdateEmailForwardingRuleAsync(
            new RegistrarEmailForwardingRuleContext(
                _connection, ToPlugin(change.Domain), new RegistrarEmailForwardingRule(change.Source, change.Destination)),
            ct);

        return Settle(nameof(UpdateEmailForwardingRuleAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct)
    {
        if (_emailForwarding is null)
        {
            return Unsupported();
        }

        var answer = await _emailForwarding.DeleteEmailForwardingRuleAsync(
            new RegistrarEmailForwardingRemovalContext(_connection, ToPlugin(removal.Domain), removal.Source), ct);

        return Settle(nameof(DeleteEmailForwardingRuleAsync), answer);
    }

    /// <inheritdoc />
    public async Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        if (_dnsManagement is null)
        {
            return Unsupported();
        }

        var answer = await _dnsManagement.SetDnsManagementAsync(
            new RegistrarFlagContext(_connection, ToPlugin(toggle.Domain), toggle.Enabled), ct);

        return Settle(nameof(SetDnsManagementAsync), answer);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A premium name is reported as not available: the host sells at the TLD's price and cannot sell a premium one yet,
    /// so the answer fails closed rather than quoting a price that is not the registrar's.
    /// </remarks>
    public async Task<Result<bool>> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        var answer = await _plugin.CheckAvailabilityAsync(new RegistrarAvailabilityContext(_connection, domainName), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } availability)
        {
            return Result<bool>.Success(
                availability.IsAvailable
                && availability.PremiumPrice is null
                && availability.Reason != RegistrarAvailabilityReason.Premium);
        }

        return ReadFailure<bool>(nameof(CheckAvailabilityAsync), answer.Outcome, answer.Reason);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A domain the plugin still reports as pending, or as an order that still requires polling, is not active yet, so it
    /// answers <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> and the sync never activates it.
    /// </remarks>
    public async Task<RegistrarResult> CheckDomainActiveAsync(DomainRef domain, CancellationToken ct)
    {
        var answer = await _plugin.CheckDomainActiveAsync(new RegistrarDomainContext(_connection, ToPlugin(domain)), ct);
        if (answer.Outcome == PanelOperationOutcome.Pending || answer.Value is { RequiresPolling: true })
        {
            return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_ACTIVE);
        }

        return Order(nameof(CheckDomainActiveAsync), answer);
    }

    /// <inheritdoc />
    public async Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        if (_whois is null)
        {
            return null;
        }

        var answer = await _whois.GetWhoisAsync(new RegistrarWhoisContext(_connection, domainName), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } whois)
        {
            return new WhoisInfo(whois.Registrar, whois.Registrant, whois.CreatedAt, whois.ExpiresAt);
        }

        LogRefusal(nameof(GetWhoisAsync), answer.Outcome, answer.Reason);

        return null;
    }

    /// <inheritdoc />
    /// <remarks>A plugin that offers no price list reads as an empty one; any other refusal is the failure code.</remarks>
    public async Task<Result<IReadOnlyList<TldPricing>>> GetTldPricingAsync(CancellationToken ct)
    {
        var answer = await _plugin.GetTldPricingAsync(new RegistrarPricingContext(_connection), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } pricing)
        {
            return Result<IReadOnlyList<TldPricing>>.Success(
                pricing
                    .Select(p => new TldPricing(
                        p.Tld,
                        p.Currency,
                        new Dictionary<int, decimal>(p.Register),
                        new Dictionary<int, decimal>(p.Transfer),
                        new Dictionary<int, decimal>(p.Renew)))
                    .ToList());
        }

        if (answer.Outcome == PanelOperationOutcome.NotSupported)
        {
            return Result<IReadOnlyList<TldPricing>>.Success([]);
        }

        return ReadFailure<IReadOnlyList<TldPricing>>(nameof(GetTldPricingAsync), answer.Outcome, answer.Reason);
    }

    /// <inheritdoc />
    /// <remarks>A plugin that does not declare the domain list answers <c>REGISTRAR_OPERATION_UNSUPPORTED</c> and is never asked.</remarks>
    public async Task<Result<RegistrarHeldDomainPage>> ListDomainsAsync(string? cursor, CancellationToken ct)
    {
        if (_domainList is null)
        {
            return Result<RegistrarHeldDomainPage>.FailureCode(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
        }

        var answer = await _domainList.ListDomainsAsync(new RegistrarDomainListContext(_connection, cursor), ct);
        if (RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason) is null && answer.Value is { } page)
        {
            return Result<RegistrarHeldDomainPage>.Success(new RegistrarHeldDomainPage(
                page.Domains
                    .Select(d => new RegistrarHeldDomain(d.Domain.Name, d.Domain.RegistrarRef, d.ExpiresAt))
                    .ToList(),
                page.NextCursor));
        }

        return ReadFailure<RegistrarHeldDomainPage>(nameof(ListDomainsAsync), answer.Outcome, answer.Reason);
    }

    /// <summary>Names a domain the way the SDK addresses it.</summary>
    /// <param name="domain">The host's address of the domain.</param>
    /// <returns>The domain's name and the reference the registrar returned for it.</returns>
    private static RegistrarDomain ToPlugin(DomainRef domain)
    {
        return new RegistrarDomain(domain.Name, domain.RegistrarRef);
    }

    /// <summary>The answer of an optional operation the plugin does not offer.</summary>
    /// <returns>A failed result carrying <c>REGISTRAR_OPERATION_UNSUPPORTED</c>.</returns>
    private static RegistrarResult Unsupported()
    {
        return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
    }

    /// <summary>Turns a plugin's answer to an operation that places or renews a domain into a result.</summary>
    /// <param name="operation">The operation, for the log.</param>
    /// <param name="answer">What the plugin answered.</param>
    /// <returns>
    /// Success with the registrar's reference and expiry (and the polling flag, set too when the plugin accepted the order
    /// but has not finished it), or the failure code. An answer that claims success but carries no order fails closed.
    /// </returns>
    private RegistrarResult Order(string operation, ValueResult<RegistrarOrder, RegistrarRefusalReason> answer)
    {
        var code = RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason);
        if (code is null && answer.Value is { } order)
        {
            return new RegistrarResult(
                true,
                order.RegistrarRef,
                order.ExpiresAt,
                null,
                order.RequiresPolling || answer.Outcome == PanelOperationOutcome.Pending);
        }

        LogRefusal(operation, answer.Outcome, answer.Reason);

        return new RegistrarResult(false, null, null, code ?? HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>Turns a plugin's answer to an operation that changes a domain into a result.</summary>
    /// <param name="operation">The operation, for the log.</param>
    /// <param name="answer">What the plugin answered.</param>
    /// <returns>Success when the change took effect, otherwise the failure code.</returns>
    private RegistrarResult Settle(string operation, OperationResult<RegistrarRefusalReason> answer)
    {
        var code = RegistrarRefusalMap.FailureCode(answer.Outcome, answer.Reason);
        if (code is null)
        {
            return new RegistrarResult(true, null, null, null);
        }

        LogRefusal(operation, answer.Outcome, answer.Reason);

        return new RegistrarResult(false, null, null, code);
    }

    /// <summary>Turns a plugin's refusal of a read into a failed result, through the one refusal map.</summary>
    /// <typeparam name="T">The value the read would have answered.</typeparam>
    /// <param name="operation">The operation, for the log.</param>
    /// <param name="outcome">How the plugin ended it.</param>
    /// <param name="reason">The refusal reason, when there was one.</param>
    /// <returns>
    /// A failure carrying the <c>REGISTRAR_*</c> code; an answer that claims success but carries no value fails closed as
    /// <c>REGISTRAR_ORDER_UPSTREAM_FAILED</c>.
    /// </returns>
    private Result<T> ReadFailure<T>(string operation, PanelOperationOutcome outcome, RegistrarRefusalReason? reason)
    {
        LogRefusal(operation, outcome, reason);

        return Result<T>.FailureCode(
            RegistrarRefusalMap.FailureCode(outcome, reason) ?? HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }

    /// <summary>Records that a plugin did not complete an operation, by plugin id, operation, outcome and reason.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="outcome">How the plugin ended it.</param>
    /// <param name="reason">The refusal reason, when there was one.</param>
    private void LogRefusal(string operation, PanelOperationOutcome outcome, RegistrarRefusalReason? reason)
    {
        _logger.LogWarning(
            "Registrar plugin {PluginId} did not complete {Operation}: {Outcome} {Reason}.",
            _pluginId, operation, outcome, reason);
    }
}
