namespace Innovayse.Infrastructure.Domains.Namecheap;

using System.Xml.Linq;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Implements <see cref="IRegistrarProvider"/> using the Namecheap XML API v2.
/// All operations are dispatched through <see cref="NamecheapClient"/>.
/// </summary>
public sealed class NamecheapRegistrarProvider(NamecheapClient client) : IRegistrarProvider
{
    /// <summary>No-op success result returned when the registrar is not configured.</summary>
    private static readonly RegistrarResult _notConfiguredResult = new(true, null, null, null);

    /// <inheritdoc/>
    public async Task<RegistrarResult> RegisterAsync(RegisterDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        var (sld, tld) = SplitDomain(request.DomainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
            ["Years"] = request.Years.ToString(),
            ["AddFreeWhoisguard"] = request.WhoisPrivacy ? "yes" : "no",
            ["WGEnabled"] = request.WhoisPrivacy ? "yes" : "no",
        };

        if (request.Nameserver1 is not null)
        {
            parameters["Nameservers"] = BuildNameserverString(request.Nameserver1, request.Nameserver2);
        }

        var doc = await client.ExecuteAsync("namecheap.domains.create", parameters, ct);

        var ns = GetNamespace(doc);
        var result = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainCreateResult", ns));

        var domainId = result?.Attribute("DomainID")?.Value ?? string.Empty;
        var expiresAtRaw = result?.Attribute("ExpireDate")?.Value;
        DateTimeOffset expiresAt = expiresAtRaw is not null
            ? DateTimeOffset.Parse(expiresAtRaw)
            : DateTimeOffset.UtcNow.AddYears(request.Years);

        return new RegistrarResult(true, domainId, expiresAt, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> TransferAsync(TransferDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        var (sld, tld) = SplitDomain(request.DomainName);

        var parameters = new Dictionary<string, string>
        {
            ["DomainName"] = request.DomainName,
            ["EPPCode"] = request.EppCode,
            ["AddFreeWhoisguard"] = request.WhoisPrivacy ? "yes" : "no",
        };

        _ = sld;
        _ = tld;

        var doc = await client.ExecuteAsync("namecheap.domains.transfer.create", parameters, ct);

        var ns = GetNamespace(doc);
        var result = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainTransferCreateResult", ns));

        var transferId = result?.Attribute("TransferID")?.Value ?? string.Empty;
        var expiresAtRaw = result?.Attribute("ExpireDate")?.Value;
        DateTimeOffset? expiresAt = expiresAtRaw is not null
            ? DateTimeOffset.Parse(expiresAtRaw)
            : null;

        return new RegistrarResult(true, transferId, expiresAt, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> RenewAsync(RenewDomainRequest request, CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        var (sld, tld) = SplitDomain(request.DomainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
            ["Years"] = request.Years.ToString(),
        };

        var doc = await client.ExecuteAsync("namecheap.domains.renew", parameters, ct);

        var ns = GetNamespace(doc);
        var result = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainRenewResult", ns));

        var expiresAtRaw = result?.Attribute("ExpireDate")?.Value;
        DateTimeOffset? expiresAt = expiresAtRaw is not null
            ? DateTimeOffset.Parse(expiresAtRaw)
            : DateTimeOffset.UtcNow.AddYears(request.Years);

        return new RegistrarResult(true, request.RegistrarRef, expiresAt, null);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        // TODO: Namecheap does not expose a direct cancel-transfer endpoint.
        // Manual intervention via support is required for in-progress transfers.
        return Task.FromResult(new RegistrarResult(false, registrarRef, null,
            "Namecheap does not support programmatic transfer cancellation."));
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> InitiateOutgoingTransferAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        // Unlock the domain to permit outgoing transfer.
        return await SetRegistrarLockAsync(domainName, registrarRef, false, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetAutoRenewAsync(
        string domainName,
        string registrarRef,
        bool enabled,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
            ["IsAutoRenew"] = enabled ? "true" : "false",
        };

        await client.ExecuteAsync("namecheap.domains.setAutoRenew", parameters, ct);
        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetWhoisPrivacyAsync(
        string domainName,
        string registrarRef,
        bool enabled,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        // Step 1: Get the WhoisguardID from domain info
        var (sld, tld) = SplitDomain(domainName);

        var infoParams = new Dictionary<string, string>
        {
            ["DomainName"] = domainName,
        };

        var infoDoc = await client.ExecuteAsync("namecheap.domains.getInfo", infoParams, ct);
        var ns = GetNamespace(infoDoc);

        var whoisguardNode = infoDoc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainGetInfoResult", ns))
            ?.Element(XName.Get("Whoisguard", ns));

        var whoisguardId = whoisguardNode?.Element(XName.Get("ID", ns))?.Value;

        if (string.IsNullOrEmpty(whoisguardId))
        {
            return new RegistrarResult(false, registrarRef, null,
                "No WhoisGuard subscription found for this domain. Enable WhoisGuard via the Namecheap dashboard first.");
        }

        // Step 2: Enable or disable WhoisGuard using the retrieved ID
        var command = enabled ? "namecheap.whoisguard.enable" : "namecheap.whoisguard.disable";
        var parameters = new Dictionary<string, string>
        {
            ["WhoisguardID"] = whoisguardId,
            ["ForwardedToEmail"] = string.Empty,
        };

        await client.ExecuteAsync(command, parameters, ct);
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
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
            ["LockAction"] = locked ? "LOCK" : "UNLOCK",
        };

        await client.ExecuteAsync("namecheap.domains.setRegistrarLock", parameters, ct);
        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<string?> GetEppCodeAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return null;
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
        };

        var doc = await client.ExecuteAsync("namecheap.domains.getEPPCode", parameters, ct);

        var ns = GetNamespace(doc);
        var result = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainGetEPPCodeResult", ns));

        return result?.Attribute("EPPCode")?.Value ?? result?.Value;
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> SetNameserversAsync(
        string domainName,
        string registrarRef,
        IReadOnlyList<string> nameservers,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return _notConfiguredResult;
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
            ["Nameservers"] = string.Join(",", nameservers),
        };

        await client.ExecuteAsync("namecheap.domains.dns.setCustom", parameters, ct);
        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
    {
        if (!client.IsConfigured) return [];
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
        };

        var doc = await client.ExecuteAsync("namecheap.domains.dns.getHosts", parameters, ct);

        var ns = GetNamespace(doc);
        var hostNodes = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainDNSGetHostsResult", ns))
            ?.Elements(XName.Get("host", ns))
            ?? [];

        var records = new List<DnsRecord>();
        var idCounter = 1;

        foreach (var host in hostNodes)
        {
            var typeRaw = host.Attribute("Type")?.Value ?? "A";
            if (!Enum.TryParse<DnsRecordType>(typeRaw, true, out var recordType))
            {
                continue;
            }

            var hostName = host.Attribute("Name")?.Value ?? "@";
            var address = host.Attribute("Address")?.Value ?? string.Empty;
            var ttlRaw = host.Attribute("TTL")?.Value ?? "1800";
            var ttl = int.TryParse(ttlRaw, out var parsedTtl) ? parsedTtl : 1800;
            var mxPrefRaw = host.Attribute("MXPref")?.Value;
            int? priority = int.TryParse(mxPrefRaw, out var pref) ? pref : null;

            records.Add(DnsRecord.CreateDetached(recordType, hostName, address, ttl, priority));
            idCounter++;
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
        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);
        var updated = existing.ToList();
        updated.Add(record);
        return await SetAllHostsAsync(domainName, registrarRef, updated, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> UpdateDnsRecordAsync(
        string domainName,
        string registrarRef,
        DnsRecord record,
        CancellationToken ct)
    {
        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);

        var updated = existing
            .Select(r => r.Host == record.Host && r.Type == record.Type ? record : r)
            .ToList();

        return await SetAllHostsAsync(domainName, registrarRef, updated, ct);
    }

    /// <inheritdoc/>
    public async Task<RegistrarResult> DeleteDnsRecordAsync(
        string domainName,
        string registrarRef,
        int recordId,
        CancellationToken ct)
    {
        var existing = await GetDnsRecordsAsync(domainName, registrarRef, ct);

        // Namecheap has no native record IDs — skip the record at the given 1-based position index.
        var updated = existing
            .Where((_, idx) => idx + 1 != recordId)
            .ToList();

        return await SetAllHostsAsync(domainName, registrarRef, updated, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct)
    {
        if (!client.IsConfigured) return false;
        var parameters = new Dictionary<string, string>
        {
            ["DomainList"] = domainName,
        };

        var doc = await client.ExecuteAsync("namecheap.domains.check", parameters, ct);

        var ns = GetNamespace(doc);
        var result = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainCheckResult", ns));

        var available = result?.Attribute("Available")?.Value;
        return string.Equals(available, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(
        string domainName,
        DomainContact contact,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap contacts.setCustom API call.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(
        string domainName,
        bool enabled,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap email forwarding toggle.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(
        string domainName,
        string source,
        string destination,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap email forwarding rule creation.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(
        string domainName,
        string source,
        string destination,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap email forwarding rule update.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(
        string domainName,
        string source,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap email forwarding rule deletion.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(
        string domainName,
        bool enabled,
        CancellationToken ct)
    {
        // TODO: Implement Namecheap DNS management toggle.
        return Task.FromResult(new RegistrarResult(true, null, null, null));
    }

    /// <inheritdoc/>
    public async Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
    {
        if (!client.IsConfigured) return null;
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
        };

        var doc = await client.ExecuteAsync("namecheap.domains.getInfo", parameters, ct);

        var ns = GetNamespace(doc);
        var info = doc.Root
            ?.Element(XName.Get("CommandResponse", ns))
            ?.Element(XName.Get("DomainGetInfoResult", ns));

        if (info is null)
        {
            return null;
        }

        var registrar = info.Attribute("RegistrarName")?.Value ?? "Namecheap";
        var registrant = info
            .Element(XName.Get("Registrant", ns))
            ?.Element(XName.Get("OrganizationName", ns))
            ?.Value ?? string.Empty;

        var createdRaw = info.Attribute("CreatedDate")?.Value;
        var expiresRaw = info.Attribute("ExpiredDate")?.Value;

        DateTimeOffset createdAt = createdRaw is not null
            ? DateTimeOffset.Parse(createdRaw)
            : DateTimeOffset.MinValue;

        DateTimeOffset expiresAt = expiresRaw is not null
            ? DateTimeOffset.Parse(expiresRaw)
            : DateTimeOffset.MaxValue;

        return new WhoisInfo(registrar, registrant, createdAt, expiresAt);
    }

    /// <summary>
    /// Pushes the complete list of DNS host records to Namecheap using <c>namecheap.domains.dns.setHosts</c>.
    /// Namecheap requires the full record set on every write — partial updates are not supported.
    /// </summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="registrarRef">The registrar's reference for this domain.</param>
    /// <param name="records">Complete list of DNS records to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the operation succeeded.</returns>
    private async Task<RegistrarResult> SetAllHostsAsync(
        string domainName,
        string registrarRef,
        IList<DnsRecord> records,
        CancellationToken ct)
    {
        var (sld, tld) = SplitDomain(domainName);

        var parameters = new Dictionary<string, string>
        {
            ["SLD"] = sld,
            ["TLD"] = tld,
        };

        for (var i = 0; i < records.Count; i++)
        {
            var n = i + 1;
            var rec = records[i];
            parameters[$"HostName{n}"] = rec.Host;
            parameters[$"RecordType{n}"] = rec.Type.ToString();
            parameters[$"Address{n}"] = rec.Value;
            parameters[$"TTL{n}"] = rec.Ttl.ToString();

            if (rec.Priority.HasValue)
            {
                parameters[$"MXPref{n}"] = rec.Priority.Value.ToString();
            }
        }

        await client.ExecuteAsync("namecheap.domains.dns.setHosts", parameters, ct);
        return new RegistrarResult(true, registrarRef, null, null);
    }

    /// <summary>
    /// Splits a fully-qualified domain name into its second-level and top-level parts.
    /// </summary>
    /// <param name="domainName">Fully-qualified domain name (e.g. <c>example.co.uk</c>).</param>
    /// <returns>A tuple of (SLD, TLD) where SLD is the first label and TLD is the remainder.</returns>
    private static (string Sld, string Tld) SplitDomain(string domainName)
    {
        var parts = domainName.Split('.');
        var sld = parts[0];
        var tld = string.Join(".", parts[1..]);
        return (sld, tld);
    }

    /// <summary>
    /// Extracts the default XML namespace URI from the root element of a Namecheap API response document.
    /// </summary>
    /// <param name="doc">The parsed XML response document.</param>
    /// <returns>The namespace URI string, or an empty string if no namespace is declared.</returns>
    private static string GetNamespace(XDocument doc)
    {
        return doc.Root?.Name.NamespaceName ?? string.Empty;
    }

    /// <summary>
    /// Builds a comma-separated nameserver string from the provided primary and optional secondary values.
    /// </summary>
    /// <param name="ns1">Primary nameserver hostname.</param>
    /// <param name="ns2">Optional secondary nameserver hostname.</param>
    /// <returns>Comma-separated nameserver list suitable for the Namecheap API.</returns>
    private static string BuildNameserverString(string ns1, string? ns2)
    {
        return ns2 is not null ? $"{ns1},{ns2}" : ns1;
    }
}
