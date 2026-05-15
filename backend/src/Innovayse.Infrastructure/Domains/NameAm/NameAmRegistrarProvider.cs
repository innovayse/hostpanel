namespace Innovayse.Infrastructure.Domains.NameAm;

using System.Text.Json;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements <see cref="IRegistrarProvider"/> using the Name.am JSON REST API.
/// All operations are dispatched through <see cref="NameAmClient"/>.
/// </summary>
public sealed class NameAmRegistrarProvider(NameAmClient client, ILogger<NameAmRegistrarProvider> logger)
    : IRegistrarProvider
{
    /// <summary>No-op success result returned when the registrar is not configured.</summary>
    private static readonly RegistrarResult _notConfiguredResult = new(true, null, null, null);

    /// <inheritdoc/>
    public async Task<RegistrarResult> RegisterAsync(RegisterDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var (name, tld) = SplitDomain(request.DomainName);

        var nameservers = BuildNameserverList(request.Nameserver1, request.Nameserver2);
        var contacts = CreateDefaultContacts();

        var purchasePayload = new[]
        {
            new Dictionary<string, object>
            {
                ["name"] = tld,
                ["type"] = "domain_registration",
                ["domain"] = request.DomainName,
                ["plan"] = new { _id = $"{request.Years}_year_register" },
                ["registrantContacts"] = contacts,
                ["administrativeContacts"] = contacts,
                ["technicalContacts"] = contacts,
                ["billingContacts"] = contacts,
                ["nameServers"] = nameservers,
            },
        };

        logger.LogInformation("Registering domain {DomainName} for {Years} years via Name.am",
            request.DomainName, request.Years);

        using var doc = await client.PostAsync("/client/carts/purchase", purchasePayload, ct);

        var domainRef = request.DomainName;
        var expiresAt = DateTimeOffset.UtcNow.AddYears(request.Years);

        return new RegistrarResult(true, domainRef, expiresAt, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> TransferAsync(TransferDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var (name, tld) = SplitDomain(request.DomainName);
        var contacts = CreateDefaultContacts();

        var purchasePayload = new[]
        {
            new Dictionary<string, object>
            {
                ["name"] = tld,
                ["type"] = "domain_transfer",
                ["domain"] = request.DomainName,
                ["plan"] = new { _id = "1_year_transfer" },
                ["transferCode"] = request.EppCode,
                ["registrantContacts"] = contacts,
                ["administrativeContacts"] = contacts,
                ["technicalContacts"] = contacts,
                ["billingContacts"] = contacts,
            },
        };

        logger.LogInformation("Initiating transfer for domain {DomainName} via Name.am", request.DomainName);

        using var doc = await client.PostAsync("/client/carts/purchase", purchasePayload, ct);

        return new RegistrarResult(true, request.DomainName, null, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> RenewAsync(RenewDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var (name, tld) = SplitDomain(request.DomainName);

        var purchasePayload = new[]
        {
            new Dictionary<string, object>
            {
                ["name"] = tld,
                ["type"] = "domain_renew",
                ["domain"] = request.DomainName,
                ["plan"] = new { _id = $"{request.Years}_year_renew" },
            },
        };

        logger.LogInformation("Renewing domain {DomainName} for {Years} years via Name.am",
            request.DomainName, request.Years);

        using var doc = await client.PostAsync("/client/carts/purchase", purchasePayload, ct);

        var expiresAt = DateTimeOffset.UtcNow.AddYears(request.Years);

        return new RegistrarResult(true, request.RegistrarRef, expiresAt, null);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        return Task.FromResult(new RegistrarResult(false, registrarRef, null,
            "Name.am does not support programmatic transfer cancellation."));
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> InitiateOutgoingTransferAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        // Unlock the domain to permit outgoing transfer.
        return await SetRegistrarLockAsync(domainName, registrarRef, false, ct);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(
        string domainName,
        string registrarRef,
        bool enabled,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return Task.FromResult(_notConfiguredResult);

        // Name.am manages auto-renew on their side; no dedicated API endpoint.
        logger.LogInformation("Auto-renew for {DomainName} set to {Enabled} (managed by Name.am)",
            domainName, enabled);

        return Task.FromResult(new RegistrarResult(true, registrarRef, null, null));
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetWhoisPrivacyAsync(
        string domainName,
        string registrarRef,
        bool enabled,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var payload = new { whoIsPrivacyStatus = enabled };

        logger.LogInformation("Setting WHOIS privacy for {DomainName} to {Enabled} via Name.am",
            domainName, enabled);

        using var doc = await client.PutAsync($"/client/domains/{domainName}", payload, ct);

        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetRegistrarLockAsync(
        string domainName,
        string registrarRef,
        bool locked,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var payload = new { transferLock = locked };

        logger.LogInformation("Setting registrar lock for {DomainName} to {Locked} via Name.am",
            domainName, locked);

        using var doc = await client.PutAsync($"/client/domains/{domainName}", payload, ct);

        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<string?> GetEppCodeAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return null;

        logger.LogInformation("Retrieving EPP code for {DomainName} via Name.am", domainName);

        using var doc = await client.GetAsync($"/client/domains/{domainName}/transfer", ct);

        if (doc.RootElement.TryGetProperty("transferCode", out var transferCodeEl))
        {
            return transferCodeEl.GetString();
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetNameserversAsync(
        string domainName,
        string registrarRef,
        IReadOnlyList<string> nameservers,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var nsList = nameservers.Select(ns => new { hostname = ns }).ToArray();
        var payload = new { nameServers = nsList };

        logger.LogInformation("Setting {Count} nameservers for {DomainName} via Name.am",
            nameservers.Count, domainName);

        using var doc = await client.PutAsync($"/client/domains/{domainName}", payload, ct);

        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return [];

        using var doc = await client.GetAsync("/client/domains", ct);

        var domainEl = FindDomainInDocs(doc, domainName);
        if (domainEl is null)
        {
            logger.LogWarning("Domain {DomainName} not found in Name.am domain list", domainName);
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
            var typeRaw = rec.GetProperty("type").GetString() ?? "A";
            if (!Enum.TryParse<DnsRecordType>(typeRaw, true, out var recordType))
            {
                continue;
            }

            var host = rec.GetProperty("name").GetString() ?? "@";
            var value = rec.GetProperty("content").GetString() ?? string.Empty;
            var ttl = rec.TryGetProperty("ttl", out var ttlEl) ? ttlEl.GetInt32() : 1800;
            int? priority = rec.TryGetProperty("priority", out var prioEl) &&
                            prioEl.ValueKind == JsonValueKind.Number
                ? prioEl.GetInt32()
                : null;

            records.Add(DnsRecord.CreateDetached(recordType, host, value, ttl, priority));
        }

        return records.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> AddDnsRecordAsync(
        string domainName,
        string registrarRef,
        DnsRecord record,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);
        var allRecords = existing.ToList();
        allRecords.Add(record);

        return await SetAllDnsRecordsAsync(domainName, allRecords, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> UpdateDnsRecordAsync(
        string domainName,
        string registrarRef,
        DnsRecord record,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);

        var updated = existing
            .Select(r => r.Host == record.Host && r.Type == record.Type ? record : r)
            .ToList();

        return await SetAllDnsRecordsAsync(domainName, updated, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> DeleteDnsRecordAsync(
        string domainName,
        string registrarRef,
        int recordId,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);

        // Name.am has no stable record IDs in our model — skip the record at the given 1-based index.
        var updated = existing
            .Where((_, idx) => idx + 1 != recordId)
            .ToList();

        return await SetAllDnsRecordsAsync(domainName, updated, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        if (!client.IsConfigured) return false;

        var (name, tld) = SplitDomain(domainName);

        var checkPayload = new[]
        {
            new { tld, domain = domainName },
        };

        logger.LogInformation("Checking availability for {DomainName} via Name.am", domainName);

        using var doc = await client.PostAsync("/client/domains/check", checkPayload, ct);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var domainVal = item.GetProperty("domain").GetString();
                if (string.Equals(domainVal, domainName, StringComparison.OrdinalIgnoreCase))
                {
                    return item.GetProperty("available").GetBoolean();
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        if (!client.IsConfigured) return null;

        using var doc = await client.GetAsync("/client/domains", ct);

        var domainEl = FindDomainInDocs(doc, domainName);
        if (domainEl is null)
        {
            return null;
        }

        var registrant = string.Empty;

        if (domainEl.Value.TryGetProperty("registrantContacts", out var contactsEl) &&
            contactsEl.ValueKind == JsonValueKind.Object)
        {
            var firstName = contactsEl.TryGetProperty("firstName", out var fnEl)
                ? fnEl.GetString() ?? string.Empty
                : string.Empty;
            var lastName = contactsEl.TryGetProperty("lastName", out var lnEl)
                ? lnEl.GetString() ?? string.Empty
                : string.Empty;
            var org = contactsEl.TryGetProperty("organization", out var orgEl)
                ? orgEl.GetString()
                : null;

            registrant = !string.IsNullOrWhiteSpace(org) ? org : $"{firstName} {lastName}".Trim();
        }

        DateTimeOffset createdAt = DateTimeOffset.MinValue;
        DateTimeOffset expiresAt = DateTimeOffset.MaxValue;

        if (domainEl.Value.TryGetProperty("registered", out var regEl))
        {
            var regStr = regEl.GetString();
            if (regStr is not null && DateTimeOffset.TryParse(regStr, out var parsed))
            {
                createdAt = parsed;
            }
        }

        if (domainEl.Value.TryGetProperty("expiration", out var expEl))
        {
            var expStr = expEl.GetString();
            if (expStr is not null && DateTimeOffset.TryParse(expStr, out var parsed))
            {
                expiresAt = parsed;
            }
        }

        return new WhoisInfo("Name.am", registrant, createdAt, expiresAt);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> ModifyContactDetailsAsync(
        string domainName,
        DomainContact contact,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;

        var contactObj = MapDomainContact(contact);

        var payload = new
        {
            registrantContacts = contactObj,
            administrativeContacts = contactObj,
            technicalContacts = contactObj,
            billingContacts = contactObj,
        };

        logger.LogInformation("Modifying contact details for {DomainName} via Name.am", domainName);

        using var doc = await client.PutAsync($"/client/domains/{domainName}", payload, ct);

        return new RegistrarResult(true, null, null, null);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(
        string domainName,
        bool enabled,
        CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(
        string domainName,
        string source,
        string destination,
        CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(
        string domainName,
        string source,
        string destination,
        CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(
        string domainName,
        string source,
        CancellationToken ct)
    {
        // Name.am does not expose an email forwarding API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(
        string domainName,
        bool enabled,
        CancellationToken ct)
    {
        // Name.am does not expose a separate DNS management toggle API.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <summary>
    /// Pushes the complete list of DNS records to Name.am via <c>PUT /client/domains/{domainName}</c>.
    /// Name.am requires all records to be sent with <c>action: "CREATE"</c> on each record.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="records">Complete list of DNS records to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the operation succeeded.</returns>
    private async Task<RegistrarResult> SetAllDnsRecordsAsync(
        string domainName,
        IList<DnsRecord> records,
        CancellationToken ct)
    {
        var apiRecords = records.Select(r => new Dictionary<string, object?>
        {
            ["type"] = r.Type.ToString(),
            ["name"] = r.Host,
            ["content"] = r.Value,
            ["ttl"] = r.Ttl,
            ["action"] = "CREATE",
            ["priority"] = r.Priority,
        }).ToArray();

        var payload = new { records = apiRecords };

        logger.LogInformation("Setting {Count} DNS records for {DomainName} via Name.am",
            records.Count, domainName);

        using var doc = await client.PutAsync($"/client/domains/{domainName}", payload, ct);

        return new RegistrarResult(true, null, null, null);
    }

    /// <summary>
    /// Finds a domain element within the <c>docs</c> array of a Name.am <c>GET /client/domains</c> response.
    /// </summary>
    /// <param name="doc">The parsed JSON response from <c>GET /client/domains</c>.</param>
    /// <param name="domainName">The fully-qualified domain name to locate.</param>
    /// <returns>The matching <see cref="JsonElement"/> or <see langword="null"/> if not found.</returns>
    private static JsonElement? FindDomainInDocs(JsonDocument doc, string domainName)
    {
        if (!doc.RootElement.TryGetProperty("docs", out var docsEl) ||
            docsEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in docsEl.EnumerateArray())
        {
            if (item.TryGetProperty("domain", out var domainProp))
            {
                var fullDomain = domainProp.GetString();

                // The API may return just the SLD in "domain" and the TLD separately.
                if (item.TryGetProperty("tld", out var tldProp))
                {
                    var sld = fullDomain;
                    var tld = tldProp.GetString();
                    fullDomain = $"{sld}.{tld}";
                }

                if (string.Equals(fullDomain, domainName, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Splits a fully-qualified domain name into its second-level and top-level parts.
    /// </summary>
    /// <param name="domainName">Fully-qualified domain name (e.g. <c>example.am</c>).</param>
    /// <returns>A tuple of (Name, TLD) where Name is the first label and TLD is the remainder.</returns>
    private static (string Name, string Tld) SplitDomain(string domainName)
    {
        var parts = domainName.Split('.');
        var name = parts[0];
        var tld = string.Join(".", parts[1..]);
        return (name, tld);
    }

    /// <summary>
    /// Maps a <see cref="DomainContact"/> value object to the Name.am API contact object format.
    /// </summary>
    /// <param name="contact">Domain contact to map.</param>
    /// <returns>Anonymous object matching the Name.am API contact schema.</returns>
    private static object MapDomainContact(DomainContact contact)
    {
        return new
        {
            firstName = contact.FirstName,
            lastName = contact.LastName,
            organization = contact.Organization ?? string.Empty,
            email = contact.Email,
            phone = contact.Phone,
            address1 = contact.Address1,
            address2 = contact.Address2 ?? string.Empty,
            city = contact.City,
            state = contact.State,
            postalCode = contact.PostalCode,
            country = contact.Country,
        };
    }

    /// <summary>
    /// Builds a list of nameserver objects in the format expected by the Name.am API.
    /// </summary>
    /// <param name="ns1">Primary nameserver hostname.</param>
    /// <param name="ns2">Optional secondary nameserver hostname.</param>
    /// <returns>Array of anonymous objects with <c>hostname</c> property.</returns>
    private static object[] BuildNameserverList(string? ns1, string? ns2)
    {
        List<object> list = [];

        if (ns1 is not null)
        {
            list.Add(new { hostname = ns1 });
        }

        if (ns2 is not null)
        {
            list.Add(new { hostname = ns2 });
        }

        return [.. list];
    }

    /// <summary>
    /// Creates a default contact object for registration requests where no contact is provided.
    /// Uses empty strings for fields that will be populated by the registrar.
    /// </summary>
    /// <returns>Anonymous object matching the Name.am API contact schema with empty defaults.</returns>
    private static object CreateDefaultContacts()
    {
        return new
        {
            firstName = "",
            lastName = "",
            organization = "",
            email = "",
            phone = "",
            address1 = "",
            address2 = "",
            city = "",
            state = "",
            postalCode = "",
            country = "",
        };
    }
}
