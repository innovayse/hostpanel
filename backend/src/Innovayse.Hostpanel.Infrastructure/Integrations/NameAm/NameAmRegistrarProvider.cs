using System.Globalization;
using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;

/// <summary>
/// Implements <see cref="IRegistrarProvider"/> using the Name.am JSON REST API.
/// All operations are dispatched through <see cref="NameAmClient"/>.
/// </summary>
public sealed class NameAmRegistrarProvider : IRegistrarProvider
{
    /// <summary>The <c>status</c> value Name.am reports for a domain that is live.</summary>
    private const string ActiveDomainStatus = "active";

    /// <summary>The behaviour text Name.am gives a registration plan.</summary>
    private const string RegisterBehavior = "register";

    /// <summary>The behaviour text Name.am gives a transfer plan.</summary>
    private const string TransferBehavior = "transfer";

    /// <summary>The behaviour text Name.am gives a renewal plan.</summary>
    private const string RenewBehavior = "renew";

    /// <summary>The cart item type Name.am buys a new registration with.</summary>
    private const string RegistrationItemType = "domain_registration";

    /// <summary>The cart item type Name.am buys an incoming transfer with.</summary>
    private const string TransferItemType = "domain_transfer";

    /// <summary>The cart item type Name.am buys a renewal with.</summary>
    private const string RenewalItemType = "domain_renew";

    /// <summary>The write action Name.am expects on each record of a zone that is replaced as a whole.</summary>
    private const string CreateRecordAction = "CREATE";

    /// <summary>
    /// The term of the plan an incoming transfer is bought on. A transfer carries a one year
    /// extension, so it is bought on the one year transfer plan whatever period the order records.
    /// </summary>
    private const int TransferTermYears = 1;

    /// <summary>The currency Name.am prices in, whatever its answer says.</summary>
    private const string PricingCurrency = "AMD";

    /// <summary>
    /// Wording in a refusal message that says the name cannot be bought. Name.am sends no error
    /// code, so the message text is the only signal besides the HTTP status.
    /// </summary>
    private static readonly string[] DomainUnavailableMarkers = ["not available", "unavailable", "already registered", "taken"];

    /// <summary>The Name.am API client every registrar call goes through.</summary>
    private readonly NameAmClient _client;

    /// <summary>Records registrar failures and skipped records without putting their detail on the wire.</summary>
    private readonly ILogger<NameAmRegistrarProvider> _logger;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="NameAmRegistrarProvider"/> class.</summary>
    /// <param name="client">The Name.am API client every registrar call goes through.</param>
    /// <param name="logger">Records registrar failures and skipped records without putting their detail on the wire.</param>
    /// <param name="clock">The source of the current moment.</param>
    public NameAmRegistrarProvider(NameAmClient client, ILogger<NameAmRegistrarProvider> logger, IClock clock)
    {
        _client = client;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// The answer to a purchase or a change when the registrar is not configured: a refusal, never
    /// a no-op success. A paid order is then refunded, a paid renewal retried, and a handler does
    /// not save a change the registrar never made. Domains not held at a registrar never get here:
    /// the handlers skip the registrar for them.
    /// </summary>
    private static readonly RegistrarResult NotConfiguredResult =
        new(false, null, null, HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);

    /// <summary>
    /// The answer to a change Name.am did not make: it failed, answered something unreadable or
    /// timed out. The registrar's own wording is logged where it is caught, never returned.
    /// </summary>
    private static readonly RegistrarResult UpstreamFailedResult =
        new(false, null, null, HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);

    /// <inheritdoc/>
    public async Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        if (!request.Contact.IsCompleteForPurchase())
        {
            _logger.LogWarning("The registrant contact for '{DomainName}' is incomplete; nothing was ordered", request.DomainName);
            return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED);
        }

        var contact = MapContact(request.Contact);
        var item = new NameAmPurchaseItemWire(
            RegistrarDomainName.Split(request.DomainName).Tld,
            RegistrationItemType,
            request.DomainName,
            PlanFor(DomainAction.Register, request.Years),
            RegistrantContacts: contact,
            AdministrativeContacts: contact,
            TechnicalContacts: contact,
            BillingContacts: contact,
            NameServers: BuildNameserverList(request.Nameserver1, request.Nameserver2));

        _logger.LogInformation("Registering domain {DomainName} for {Years} years via Name.am",
            request.DomainName, request.Years);

        var errorCode = await PurchaseAsync(item, "registration", request.DomainName, ct);
        if (errorCode is not null && !await IsHeldAsync(request.DomainName, errorCode, ct))
        {
            return new RegistrarResult(false, null, null, errorCode);
        }

        // In test mode Name.am never activates the domain, so there is nothing to poll for and
        // the handler activates it at once.
        return new RegistrarResult(
            true, request.DomainName, _clock.UtcNow.AddYears(request.Years), null, RequiresPolling: !_client.IsTestMode);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> TransferAsync(DomainTransferOrder request, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        if (!request.Contact.IsCompleteForPurchase())
        {
            _logger.LogWarning("The registrant contact for '{DomainName}' is incomplete; nothing was ordered", request.DomainName);
            return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED);
        }

        var contact = MapContact(request.Contact);
        var item = new NameAmPurchaseItemWire(
            RegistrarDomainName.Split(request.DomainName).Tld,
            TransferItemType,
            request.DomainName,
            PlanFor(DomainAction.Transfer, TransferTermYears),
            TransferCode: request.EppCode,
            RegistrantContacts: contact,
            AdministrativeContacts: contact,
            TechnicalContacts: contact,
            BillingContacts: contact);

        _logger.LogInformation("Initiating transfer for domain {DomainName} via Name.am", request.DomainName);

        var errorCode = await PurchaseAsync(item, "transfer", request.DomainName, ct);
        if (errorCode is not null && !await IsHeldAsync(request.DomainName, errorCode, ct))
        {
            return new RegistrarResult(false, null, null, errorCode);
        }

        return new RegistrarResult(true, request.DomainName, null, null, RequiresPolling: true);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Unlike a registration or a transfer, a refused renewal is never turned into success because
    /// the account holds the domain: holding it says nothing about whether it was renewed.
    /// </remarks>
    public async Task<RegistrarResult> RenewAsync(DomainRenewalOrder request, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        var item = new NameAmPurchaseItemWire(
            RegistrarDomainName.Split(request.DomainName).Tld,
            RenewalItemType,
            request.DomainName,
            PlanFor(DomainAction.Renew, request.Years));

        _logger.LogInformation("Renewing domain {DomainName} for {Years} years via Name.am",
            request.DomainName, request.Years);

        var errorCode = await PurchaseAsync(item, "renewal", request.DomainName, ct);
        if (errorCode is not null)
        {
            return new RegistrarResult(false, null, null, errorCode);
        }

        // The purchase response carries no expiry. Reporting none lets the caller extend the
        // current one; now + years would cost an early renewal its remaining days.
        return new RegistrarResult(true, request.RegistrarRef, null, null);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(DomainRef domain, CancellationToken ct)
    {
        // Name.am has no cancel-transfer endpoint; an in-progress transfer is cancelled through its support.
        return Task.FromResult(new RegistrarResult(false, domain.RegistrarRef, null,
            HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED));
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> InitiateOutgoingTransferAsync(DomainRef domain, CancellationToken ct)
    {
        // Unlock the domain to permit outgoing transfer.
        return await SetRegistrarLockAsync(new RegistrarFlagChange(domain, false), ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetAutoRenewAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        // Name.am manages auto-renew on their side; no dedicated API endpoint.
        _logger.LogInformation("Auto-renew for {DomainName} set to {Enabled} (managed by Name.am)",
            change.Domain.Name, change.Enabled);

        return new RegistrarResult(true, change.Domain.RegistrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetWhoisPrivacyAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        _logger.LogInformation("Setting WHOIS privacy for {DomainName} to {Enabled} via Name.am",
            change.Domain.Name, change.Enabled);

        return await UpdateDomainAsync(
            change.Domain.Name, change.Domain.RegistrarRef, new NameAmDomainUpdateWire(WhoIsPrivacyStatus: change.Enabled), ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetRegistrarLockAsync(RegistrarFlagChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        _logger.LogInformation("Setting registrar lock for {DomainName} to {Locked} via Name.am",
            change.Domain.Name, change.Enabled);

        return await UpdateDomainAsync(
            change.Domain.Name, change.Domain.RegistrarRef, new NameAmDomainUpdateWire(TransferLock: change.Enabled), ct);
    }

    /// <inheritdoc/>
    public async Task<string?> GetEppCodeAsync(DomainRef domain, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return null;
        }

        _logger.LogInformation("Retrieving EPP code for {DomainName} via Name.am", domain.Name);

        using var doc = await _client.GetAsync($"{DomainPath(domain.Name)}/transfer", ct);

        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("transferCode", out var transferCodeEl)
            && transferCodeEl.ValueKind == JsonValueKind.String)
        {
            return transferCodeEl.GetString();
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetNameserversAsync(NameserverChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        _logger.LogInformation("Setting {Count} nameservers for {DomainName} via Name.am",
            change.Nameservers.Count, change.Domain.Name);

        var payload = new NameAmDomainUpdateWire(
            NameServers: [.. change.Nameservers.Select(ns => new NameAmNameServerWire(ns))]);

        return await UpdateDomainAsync(change.Domain.Name, change.Domain.RegistrarRef, payload, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(DomainRef domain, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return [];
        }

        var domainEl = await FindDomainAsync(domain.Name, ct);
        if (domainEl is null)
        {
            _logger.LogWarning("Domain {DomainName} not found in Name.am domain list", domain.Name);
            return [];
        }

        if (!domainEl.Value.TryGetProperty("records", out var recordsEl) ||
            recordsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<DnsRecord> records = [];

        foreach (var rec in recordsEl.EnumerateArray())
        {
            var typeRaw = ReadString(rec, "type") ?? "A";
            if (!Enum.TryParse<DnsRecordType>(typeRaw, true, out var recordType))
            {
                continue;
            }

            var host = ReadString(rec, "name") ?? "@";
            var value = ReadString(rec, "content") ?? string.Empty;
            var ttl = rec.TryGetProperty("ttl", out var ttlEl) && ttlEl.ValueKind == JsonValueKind.Number && ttlEl.TryGetInt32(out var parsedTtl)
                ? parsedTtl
                : 1800;
            int? priority = rec.TryGetProperty("priority", out var prioEl) &&
                            prioEl.ValueKind == JsonValueKind.Number &&
                            prioEl.TryGetInt32(out var parsedPriority)
                ? parsedPriority
                : null;

            records.Add(DnsRecord.CreateDetached(recordType, host, value, ttl, priority));
        }

        return records.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> AddDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        return await GuardAsync("adding a DNS record", async () =>
        {
            var existing = await GetDnsRecordsAsync(change.Domain, ct);
            var allRecords = existing.ToList();
            allRecords.Add(change.Record);

            return await SetAllDnsRecordsAsync(change.Domain.Name, allRecords, ct);
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> UpdateDnsRecordAsync(DnsRecordChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        return await GuardAsync("updating a DNS record", async () =>
        {
            var existing = await GetDnsRecordsAsync(change.Domain, ct);

            var updated = existing
                .Select(r => r.Host == change.Record.Host && r.Type == change.Record.Type ? change.Record : r)
                .ToList();

            return await SetAllDnsRecordsAsync(change.Domain.Name, updated, ct);
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> DeleteDnsRecordAsync(DnsRecordRemoval removal, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        return await GuardAsync("deleting a DNS record", async () =>
        {
            var existing = await GetDnsRecordsAsync(removal.Domain, ct);

            // Name.am has no stable record IDs in our model: skip the record at the given 1-based index.
            var updated = existing
                .Where((_, idx) => idx + 1 != removal.RecordId)
                .ToList();

            return await SetAllDnsRecordsAsync(removal.Domain.Name, updated, ct);
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> CheckDomainActiveAsync(string domainName, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        _logger.LogInformation("Polling Name.am for active status of domain {DomainName}", domainName);

        return await GuardAsync("checking a domain's status", async () =>
        {
            var domainEl = await FindDomainAsync(domainName, ct);
            if (domainEl is null)
            {
                _logger.LogInformation("Domain {DomainName} not yet found in Name.am account", domainName);
                return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND);
            }

            // Name.am reports status in the "status" field; "active" means the domain is live.
            var status = ReadString(domainEl.Value, "status") ?? string.Empty;
            if (!string.Equals(status, ActiveDomainStatus, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Domain {DomainName} found in Name.am but status is '{Status}', so it is not yet active",
                    domainName, status);
                return new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_ACTIVE);
            }

            var expiresAt = RegistrarDate.Parse(ReadString(domainEl.Value, "expiration")) ?? _clock.UtcNow.AddYears(1);

            _logger.LogInformation(
                "Domain {DomainName} is now active in Name.am (expires {ExpiresAt:u})",
                domainName, expiresAt);

            return new RegistrarResult(true, domainName, expiresAt, null);
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            // Not "taken": unknown. A change is refused with a failed result, but a yes/no
            // question has no such answer: a storefront that reports every domain as taken
            // sells nothing and looks like it is working. Surfacing it means an operator who forgot to
            // configure a registrar finds out, instead of quietly selling nothing.
            throw new InvalidOperationException(
                "Domain availability cannot be checked because no registrar is configured. "
                + "Set the registrar credentials in Admin -> Integrations.");
        }

        NameAmDomainCheckWire[] checkPayload =
        [
            new(RegistrarDomainName.Split(domainName).Tld, domainName),
        ];

        _logger.LogInformation("Checking availability for {DomainName} via Name.am", domainName);

        using var doc = await _client.PostAsync("/client/domains/check", checkPayload, ct);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (string.Equals(ReadString(item, "domain"), domainName, StringComparison.OrdinalIgnoreCase)
                    && item.TryGetProperty("available", out var availableEl)
                    && availableEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return availableEl.GetBoolean();
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return null;
        }

        var domainEl = await FindDomainAsync(domainName, ct);
        if (domainEl is null)
        {
            return null;
        }

        var registrant = string.Empty;

        if (domainEl.Value.TryGetProperty("registrantContacts", out var contactsEl) &&
            contactsEl.ValueKind == JsonValueKind.Object)
        {
            var firstName = ReadString(contactsEl, "firstName") ?? string.Empty;
            var lastName = ReadString(contactsEl, "lastName") ?? string.Empty;
            var org = ReadString(contactsEl, "organization");

            registrant = !string.IsNullOrWhiteSpace(org) ? org : $"{firstName} {lastName}".Trim();
        }

        var createdAt = RegistrarDate.Parse(ReadString(domainEl.Value, "registered")) ?? DateTimeOffset.MinValue;
        var expiresAt = RegistrarDate.Parse(ReadString(domainEl.Value, "expiration")) ?? DateTimeOffset.MaxValue;

        return new WhoisInfo("Name.am", registrant, createdAt, expiresAt);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> ModifyContactDetailsAsync(DomainContactChange change, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return NotConfiguredResult;
        }

        var contact = MapContact(change.Contact);
        var payload = new NameAmDomainUpdateWire(
            RegistrantContacts: contact,
            AdministrativeContacts: contact,
            TechnicalContacts: contact,
            BillingContacts: contact);

        _logger.LogInformation("Modifying contact details for {DomainName} via Name.am", change.DomainName);

        return await UpdateDomainAsync(change.DomainName, null, payload, ct);
    }

    /// <inheritdoc/>
    public async Task<DomainContact?> GetContactDetailsAsync(string domainName, CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return null;
        }

        var domainEl = await FindDomainAsync(domainName, ct);
        if (domainEl is null ||
            !domainEl.Value.TryGetProperty("registrantContacts", out var contactEl) ||
            contactEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var organization = ReadString(contactEl, "organization") ?? string.Empty;
        var address2 = ReadString(contactEl, "address2") ?? string.Empty;

        return new DomainContact(
            ReadString(contactEl, "firstName") ?? string.Empty,
            ReadString(contactEl, "lastName") ?? string.Empty,
            organization.Length == 0 ? null : organization,
            ReadString(contactEl, "email") ?? string.Empty,
            ReadString(contactEl, "phone") ?? string.Empty,
            ReadString(contactEl, "address1") ?? string.Empty,
            address2.Length == 0 ? null : address2,
            ReadString(contactEl, "city") ?? string.Empty,
            ReadString(contactEl, "state") ?? string.Empty,
            ReadString(contactEl, "zip") ?? string.Empty,
            ReadString(contactEl, "country") ?? string.Empty);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(EmailForwardingRuleChange change, CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(EmailForwardingRuleRemoval removal, CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(DomainFeatureToggle toggle, CancellationToken ct)
    {
        // Name.am does not expose a separate DNS management toggle API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TldPricing>> GetTldPricingAsync(CancellationToken ct)
    {
        if (!await _client.IsConfiguredAsync(ct))
        {
            return [];
        }

        _logger.LogInformation("Fetching TLD pricing from Name.am");

        using var doc = await _client.GetAsync("/client/products", ct);

        List<TldPricing> result = [];

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Name.am /client/products returned unexpected format");
            return result;
        }

        foreach (var product in doc.RootElement.EnumerateArray())
        {
            var pricing = ReadProductPricing(product);
            if (pricing is not null)
            {
                result.Add(pricing);
            }
        }

        _logger.LogInformation("Loaded pricing for {Count} TLDs from Name.am", result.Count);

        return result;
    }

    /// <summary>
    /// Reads the prices of one product of the <c>GET /client/products</c> list. A product, or a
    /// plan within it, that does not have the expected shape is skipped, so one bad TLD does not
    /// stop the rest from being priced.
    /// </summary>
    /// <param name="product">One element of the products array.</param>
    /// <returns>The TLD's prices, or <see langword="null"/> when the product names no TLD or has no readable plan.</returns>
    private TldPricing? ReadProductPricing(JsonElement product)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var tld = ReadString(product, "name");
        if (string.IsNullOrWhiteSpace(tld)
            || !product.TryGetProperty("plans", out var plansEl)
            || plansEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Dictionary<int, decimal> registerPrices = [];
        Dictionary<int, decimal> transferPrices = [];
        Dictionary<int, decimal> renewPrices = [];

        foreach (var plan in plansEl.EnumerateArray())
        {
            var duration = plan.ValueKind == JsonValueKind.Object
                && plan.TryGetProperty("duration", out var durationEl)
                && durationEl.ValueKind == JsonValueKind.Number
                && durationEl.TryGetInt32(out var d)
                    ? d
                    : 1;

            var price = plan.ValueKind == JsonValueKind.Object && plan.TryGetProperty("currentPrice", out var priceEl)
                ? ReadPrice(priceEl)
                : null;
            if (price is null)
            {
                _logger.LogWarning("Name.am gave no readable price for a plan of '{Tld}'; the plan is skipped", tld);
                continue;
            }

            switch (ReadString(plan, "behavior"))
            {
                case RegisterBehavior:
                    registerPrices[duration] = price.Value;
                    break;
                case TransferBehavior:
                    transferPrices[duration] = price.Value;
                    break;
                case RenewBehavior:
                    renewPrices[duration] = price.Value;
                    break;
            }
        }

        return new TldPricing(tld, PricingCurrency, registerPrices, transferPrices, renewPrices);
    }

    /// <summary>
    /// Reads a price Name.am sends either as a JSON number or as a numeric string.
    /// </summary>
    /// <param name="priceEl">The <c>currentPrice</c> element.</param>
    /// <returns>The price, or <see langword="null"/> when it is neither a number nor a numeric string.</returns>
    private static decimal? ReadPrice(JsonElement priceEl)
    {
        switch (priceEl.ValueKind)
        {
            case JsonValueKind.Number when priceEl.TryGetDecimal(out var number):
                return number;
            case JsonValueKind.String when decimal.TryParse(
                priceEl.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var text):
                return text;
            default:
                return null;
        }
    }

    /// <summary>
    /// Runs a change that is not a purchase and turns every way the registrar call can fail into a
    /// failed result, so the handler answers with a code instead of an exception carrying the
    /// registrar's wording.
    /// </summary>
    /// <param name="operation">What is being done, for the log.</param>
    /// <param name="action">The change to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The change's result, or <see cref="UpstreamFailedResult"/> when the registrar call failed.</returns>
    private async Task<RegistrarResult> GuardAsync(string operation, Func<Task<RegistrarResult>> action, CancellationToken ct)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (RegistrarFailure.IsUpstream(ex, ct))
        {
            _logger.LogWarning(ex, "Name.am failed while {Operation}", operation);
            return UpstreamFailedResult;
        }
    }

    /// <summary>
    /// Sends one <c>PUT /client/domains/{name}</c> change.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar reference to hand back on success; <see langword="null"/> when there is none.</param>
    /// <param name="payload">What to change.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the failed result when Name.am did not make the change.</returns>
    private Task<RegistrarResult> UpdateDomainAsync(
        string domainName, string? registrarRef, NameAmDomainUpdateWire payload, CancellationToken ct)
    {
        return GuardAsync("changing a domain", async () =>
        {
            using var doc = await _client.PutAsync(DomainPath(domainName), payload, ct);
            return new RegistrarResult(true, registrarRef, null, null);
        }, ct);
    }

    /// <summary>
    /// Pushes the complete list of DNS records to Name.am via <c>PUT /client/domains/{domainName}</c>.
    /// Name.am requires all records to be sent with <c>action: "CREATE"</c> on each record.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="records">Complete list of DNS records to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the operation succeeded.</returns>
    private Task<RegistrarResult> SetAllDnsRecordsAsync(
        string domainName,
        IList<DnsRecord> records,
        CancellationToken ct)
    {
        _logger.LogInformation("Setting {Count} DNS records for {DomainName} via Name.am",
            records.Count, domainName);

        var payload = new NameAmDomainUpdateWire(
            Records: [.. records.Select(r => new NameAmDnsRecordWire(
                r.Type.ToString(), r.Host, r.Value, r.Ttl, CreateRecordAction, r.Priority))]);

        return UpdateDomainAsync(domainName, null, payload, ct);
    }

    /// <summary>
    /// Looks a domain up in the account's domain list, following the list's pages until the
    /// domain is found or the last page has been read, so a domain beyond the first page is
    /// never reported as missing.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name to locate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain element, detached from the response it came in, or <see langword="null"/> when the account does not hold it.</returns>
    private async Task<JsonElement?> FindDomainAsync(string domainName, CancellationToken ct)
    {
        for (var page = 1; ; page++)
        {
            using var doc = await _client.GetAsync($"/client/domains?page={page}", ct);

            var found = FindDomainInDocs(doc, domainName);
            if (found is not null)
            {
                return found.Value.Clone();
            }

            if (!HasNextPage(doc, page))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Reads whether the domain list has a page after the one just read: the list's
    /// <c>hasNextPage</c> flag when it sends one, otherwise its <c>totalPages</c> count.
    /// </summary>
    /// <param name="doc">The parsed JSON response from <c>GET /client/domains</c>.</param>
    /// <param name="page">The page number that was read.</param>
    /// <returns><see langword="true"/> when another page follows; a list that reports no paging is a single page.</returns>
    private static bool HasNextPage(JsonDocument doc, int page)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (doc.RootElement.TryGetProperty("hasNextPage", out var flag)
            && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return flag.GetBoolean();
        }

        return doc.RootElement.TryGetProperty("totalPages", out var total)
            && total.ValueKind == JsonValueKind.Number
            && total.TryGetInt32(out var pages)
            && page < pages;
    }

    /// <summary>
    /// Finds a domain element within the <c>docs</c> array of a Name.am <c>GET /client/domains</c> response.
    /// </summary>
    /// <param name="doc">The parsed JSON response from <c>GET /client/domains</c>.</param>
    /// <param name="domainName">The fully-qualified domain name to locate.</param>
    /// <returns>The matching <see cref="JsonElement"/> or <see langword="null"/> if not found.</returns>
    private static JsonElement? FindDomainInDocs(JsonDocument doc, string domainName)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("docs", out var docsEl)
            || docsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in docsEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fullDomain = ReadString(item, "domain");

            // The API may return just the SLD in "domain" and the TLD separately.
            var tld = ReadString(item, "tld");
            if (tld is not null)
            {
                fullDomain = $"{fullDomain}.{tld}";
            }

            if (string.Equals(fullDomain, domainName, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Maps a <see cref="DomainContact"/> value object to the Name.am API contact shape.
    /// </summary>
    /// <param name="contact">Domain contact to map.</param>
    /// <returns>The contact as Name.am's JSON carries it.</returns>
    private static NameAmContactWire MapContact(DomainContact contact)
    {
        return new NameAmContactWire(
            contact.FirstName,
            contact.LastName,
            $"{contact.FirstName} {contact.LastName}".Trim(),
            contact.Organization ?? string.Empty,
            contact.Email,
            contact.Phone,
            contact.Address1,
            contact.Address2 ?? string.Empty,
            contact.City,
            contact.State,
            contact.PostalCode,
            contact.Country);
    }

    /// <summary>
    /// Reads one string property of a JSON object, treating an absent or non-string property as
    /// absent so a partly filled answer still maps.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="name">The property name to read.</param>
    /// <returns>The property value, or <see langword="null"/> when absent or not a string.</returns>
    private static string? ReadString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var prop)
            && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
    }

    /// <summary>
    /// Builds the plan of a purchase item: Name.am names a plan by its term and behaviour, joined
    /// as <c>{years}_year_{behaviour}</c>, with the behaviour the plan reports in the products list
    /// (<c>register</c>, <c>renew</c> or <c>transfer</c>). A renewal is therefore bought on the
    /// renew plan, not on the register plan of the same term.
    /// </summary>
    /// <param name="action">The domain action the plan is bought for.</param>
    /// <param name="years">The term in years.</param>
    /// <returns>The plan reference.</returns>
    private static NameAmPlanWire PlanFor(DomainAction action, int years)
    {
        var behavior = action switch
        {
            DomainAction.Register => RegisterBehavior,
            DomainAction.Transfer => TransferBehavior,
            DomainAction.Renew => RenewBehavior,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "No Name.am plan for this action."),
        };
        return new NameAmPlanWire($"{years}_year_{behavior}");
    }

    /// <summary>
    /// Builds the URL path of one domain, with the name escaped so nothing in it can change the path.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <returns>The path, e.g. <c>/client/domains/example.am</c>.</returns>
    private static string DomainPath(string domainName)
    {
        return $"/client/domains/{Uri.EscapeDataString(domainName)}";
    }

    /// <summary>
    /// Builds the nameserver list of a registration.
    /// </summary>
    /// <param name="ns1">Primary nameserver hostname.</param>
    /// <param name="ns2">Optional secondary nameserver hostname.</param>
    /// <returns>The nameservers in the shape Name.am expects, empty when the registrar defaults apply.</returns>
    private static IReadOnlyList<NameAmNameServerWire> BuildNameserverList(string? ns1, string? ns2)
    {
        List<NameAmNameServerWire> list = [];

        if (ns1 is not null)
        {
            list.Add(new NameAmNameServerWire(ns1));
        }

        if (ns2 is not null)
        {
            list.Add(new NameAmNameServerWire(ns2));
        }

        return list;
    }

    /// <summary>
    /// Posts a cart purchase and reads Name.am's answer to it. Only an array of purchased items
    /// counts as bought: any other body, however the status reads, is not proof of a purchase.
    /// </summary>
    /// <param name="item">The item to buy.</param>
    /// <param name="action">What is bought, for the log (e.g. <c>renewal</c>).</param>
    /// <param name="domainName">The domain the purchase is for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refusal's error code, or <see langword="null"/> when the purchase went through.</returns>
    /// <exception cref="HttpRequestException">
    /// Thrown on a 5xx, a timeout or a transport failure: the outcome is unknown, so it is not a refusal.
    /// </exception>
    private async Task<string?> PurchaseAsync(
        NameAmPurchaseItemWire item, string action, string domainName, CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = await _client.PostAsync("/client/carts/purchase", new[] { item }, ct);
        }
        catch (HttpRequestException ex) when (IsDefiniteRefusal(ex.StatusCode))
        {
            // A 4xx means Name.am turned the order down, so nothing was bought: a clean refusal
            // the caller may act on. A 5xx or a timeout stays an exception, its outcome unknown.
            var status = (int)ex.StatusCode!.Value;
            _logger.LogWarning(
                ex, "Name.am refused {Action} of '{DomainName}' with HTTP {Status}", action, domainName, status);
            return RefusalCodeFor(ex.StatusCode, null);
        }

        using (doc)
        {
            var errorMessage = ExtractErrorMessage(doc);
            if (errorMessage is not null)
            {
                _logger.LogWarning(
                    "Name.am rejected {Action} of '{DomainName}': {Error}", action, domainName, errorMessage);
                return RefusalCodeFor(null, errorMessage);
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                _logger.LogWarning(
                    "Name.am answered {Action} of '{DomainName}' with an unexpected response", action, domainName);
                return HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED;
            }

            return null;
        }
    }

    /// <summary>
    /// Settles a refused registration or transfer that may be an earlier attempt of the same order
    /// whose outcome was never seen (a timeout, a 5xx): when the account already holds the name, the
    /// order went through and refusing it would refund a domain we own. The listing is read only
    /// then, and a failure to read it escapes, so an unconfirmed refusal is never reported as one.
    /// </summary>
    /// <param name="domainName">The domain that was ordered.</param>
    /// <param name="errorCode">The refusal code the purchase ended with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the account holds the domain and the order should count as placed.</returns>
    private async Task<bool> IsHeldAsync(string domainName, string errorCode, CancellationToken ct)
    {
        if (errorCode == HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS)
        {
            // An earlier attempt that went through would have been answered "taken" on the retry,
            // never "insufficient funds", so the account's contents decide nothing here.
            return false;
        }

        var held = await FindDomainAsync(domainName, ct) is not null;
        if (held)
        {
            _logger.LogWarning(
                "Name.am refused the order for '{DomainName}' but the account already holds it; counting it as placed", domainName);
        }

        return held;
    }

    /// <summary>
    /// Picks the error code of a refused purchase from what Name.am gave: 402 and 409 mean what
    /// they say, and a message is matched on the wording because Name.am sends no code.
    /// </summary>
    /// <param name="status">The HTTP status of a 4xx answer, if that is what was received.</param>
    /// <param name="message">The error message of a 200 answer, if that is what was received.</param>
    /// <returns>The specific code when the cause is recognised, otherwise the plain refusal code.</returns>
    private static string RefusalCodeFor(HttpStatusCode? status, string? message)
    {
        if (status == HttpStatusCode.PaymentRequired
            || (message is not null && message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)))
        {
            return HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS;
        }

        if (status == HttpStatusCode.Conflict
            || (message is not null && DomainUnavailableMarkers.Any(m => message.Contains(m, StringComparison.OrdinalIgnoreCase))))
        {
            return HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE;
        }

        return HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED;
    }

    /// <summary>
    /// Gets whether an HTTP status is Name.am declining a request outright (a 4xx other than
    /// 408 Request Timeout), as opposed to a failure whose outcome is unknown.
    /// </summary>
    /// <param name="status">The status of the failed response, if one was received.</param>
    /// <returns><see langword="true"/> when nothing can have been processed.</returns>
    private static bool IsDefiniteRefusal(HttpStatusCode? status) =>
        status is { } code && (int)code is >= 400 and < 500 && code != HttpStatusCode.RequestTimeout;

    /// <summary>
    /// Inspects a Name.am API response body for error indicators.
    /// Name.am may return HTTP 200 with an error object when a business rule is violated
    /// (e.g. domain already registered, invalid TLD, insufficient balance processed server-side).
    /// </summary>
    /// <param name="doc">The parsed JSON response from Name.am.</param>
    /// <returns>
    /// A human-readable error message if the response contains an error; otherwise <see langword="null"/>.
    /// </returns>
    private static string? ExtractErrorMessage(JsonDocument doc)
    {
        var root = doc.RootElement;

        // Name.am error responses are objects (not arrays) with a "message" or "error" field.
        // Successful cart purchase responses are arrays of purchased items.
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Check for explicit error fields.
        var message = ReadString(root, "message");
        if (message is not null)
        {
            return message;
        }

        var error = ReadString(root, "error");
        if (error is not null)
        {
            return error;
        }

        // Some APIs nest errors: { "data": null, "error": { "message": "text" } }
        if (root.TryGetProperty("error", out var errorObjEl) && errorObjEl.ValueKind == JsonValueKind.Object)
        {
            return ReadString(errorObjEl, "message");
        }

        return null;
    }
}
