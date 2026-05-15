namespace Innovayse.Integration.Tests.Domains;

using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Test stub for <see cref="IRegistrarProvider"/>.
/// Returns hardcoded success results without making any real network calls.
/// </summary>
internal sealed class StubRegistrarProvider : IRegistrarProvider
{
    /// <inheritdoc/>
    public Task<RegistrarResult> RegisterAsync(RegisterDomainRequest request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF-123", DateTimeOffset.UtcNow.AddYears(1), null));

    /// <inheritdoc/>
    public Task<RegistrarResult> TransferAsync(TransferDomainRequest request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-TRANSFER-456", DateTimeOffset.UtcNow.AddYears(1), null));

    /// <inheritdoc/>
    public Task<RegistrarResult> RenewAsync(RenewDomainRequest request, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-RENEW-789", DateTimeOffset.UtcNow.AddYears(1), null));

    /// <inheritdoc/>
    public Task<RegistrarResult> CancelTransferAsync(string domainName, string registrarRef, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> InitiateOutgoingTransferAsync(string domainName, string registrarRef, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetAutoRenewAsync(string domainName, string registrarRef, bool enabled, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetWhoisPrivacyAsync(string domainName, string registrarRef, bool enabled, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetRegistrarLockAsync(string domainName, string registrarRef, bool locked, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<string?> GetEppCodeAsync(string domainName, string registrarRef, CancellationToken ct)
        => Task.FromResult<string?>("STUB-EPP-CODE");

    /// <inheritdoc/>
    public Task<RegistrarResult> SetNameserversAsync(
        string domainName,
        string registrarRef,
        IReadOnlyList<string> nameservers,
        CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<IReadOnlyList<DnsRecord>> GetDnsRecordsAsync(
        string domainName,
        string registrarRef,
        CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DnsRecord>>([]);

    /// <inheritdoc/>
    public Task<RegistrarResult> AddDnsRecordAsync(
        string domainName,
        string registrarRef,
        DnsRecord record,
        CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateDnsRecordAsync(
        string domainName,
        string registrarRef,
        DnsRecord record,
        CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteDnsRecordAsync(
        string domainName,
        string registrarRef,
        int recordId,
        CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, registrarRef, null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> ModifyContactDetailsAsync(string domainName, DomainContact contact, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetEmailForwardingAsync(string domainName, bool enabled, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> AddEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> UpdateEmailForwardingRuleAsync(string domainName, string source, string destination, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> DeleteEmailForwardingRuleAsync(string domainName, string source, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<RegistrarResult> SetDnsManagementAsync(string domainName, bool enabled, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, "STUB-REF", null, null));

    /// <inheritdoc/>
    public Task<bool> CheckAvailabilityAsync(string domainName, CancellationToken ct)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<WhoisInfo?> GetWhoisAsync(string domainName, CancellationToken ct)
        => Task.FromResult<WhoisInfo?>(null);
}
