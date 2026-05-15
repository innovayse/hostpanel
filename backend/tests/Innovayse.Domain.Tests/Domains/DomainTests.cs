namespace Innovayse.Domain.Tests.Domains;

using FluentAssertions;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Events;

/// <summary>Unit tests for the <see cref="Domain"/> aggregate lifecycle.</summary>
public sealed class DomainTests
{
    // ── Register / Activate ──────────────────────────────────────────────────

    [Fact]
    public void Register_ShouldCreateDomainWithPendingRegistrationStatus()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), true, true);

        domain.Status.Should().Be(DomainStatus.PendingRegistration);
        domain.ClientId.Should().Be(1);
        domain.Name.Should().Be("example.com");
        domain.Tld.Should().Be("com");
        domain.AutoRenew.Should().BeTrue();
        domain.WhoisPrivacy.Should().BeTrue();
    }

    [Fact]
    public void Activate_AfterRegister_ShouldTransitionToActiveAndRaiseDomainRegisteredEvent()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), true, false);

        domain.Activate("REG-001");

        domain.Status.Should().Be(DomainStatus.Active);
        domain.RegistrarRef.Should().Be("REG-001");
        domain.DomainEvents.Should().ContainSingle(e => e is DomainRegisteredEvent);
        var evt = (DomainRegisteredEvent)domain.DomainEvents[0];
        evt.ClientId.Should().Be(1);
        evt.Name.Should().Be("example.com");
    }

    [Fact]
    public void Activate_WhenNotPendingRegistration_ShouldThrowInvalidOperationException()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), false, false);
        domain.Activate("REG-001");

        var act = () => domain.Activate("REG-002");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── CreateTransfer / ActivateTransfer ────────────────────────────────────

    [Fact]
    public void CreateTransfer_ShouldCreateDomainWithPendingTransferStatus()
    {
        var domain = Domain.CreateTransfer(2, "transfer.net");

        domain.Status.Should().Be(DomainStatus.PendingTransfer);
        domain.ClientId.Should().Be(2);
        domain.Name.Should().Be("transfer.net");
        domain.Tld.Should().Be("net");
    }

    [Fact]
    public void ActivateTransfer_ShouldTransitionToActiveAndRaiseDomainTransferredInEvent()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddYears(2);
        var domain = Domain.CreateTransfer(2, "transfer.net");

        domain.ActivateTransfer("REG-TR-001", expiresAt);

        domain.Status.Should().Be(DomainStatus.Active);
        domain.RegistrarRef.Should().Be("REG-TR-001");
        domain.ExpiresAt.Should().Be(expiresAt);
        domain.DomainEvents.Should().ContainSingle(e => e is DomainTransferredInEvent);
        var evt = (DomainTransferredInEvent)domain.DomainEvents[0];
        evt.ClientId.Should().Be(2);
        evt.Name.Should().Be("transfer.net");
    }

    // ── MarkExpired ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkExpired_WhenActive_ShouldTransitionToExpiredAndRaiseDomainExpiredEvent()
    {
        var domain = CreateActiveDomain();

        domain.MarkExpired();

        domain.Status.Should().Be(DomainStatus.Expired);
        domain.DomainEvents.OfType<DomainExpiredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void MarkExpired_WhenNotActive_ShouldThrowInvalidOperationException()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), false, false);

        var act = () => domain.MarkExpired();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── MarkRedemption ───────────────────────────────────────────────────────

    [Fact]
    public void MarkRedemption_WhenExpired_ShouldTransitionToRedemption()
    {
        var domain = CreateActiveDomain();
        domain.MarkExpired();

        domain.MarkRedemption();

        domain.Status.Should().Be(DomainStatus.Redemption);
    }

    [Fact]
    public void MarkRedemption_WhenNotExpired_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.MarkRedemption();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── MarkTransferred ──────────────────────────────────────────────────────

    [Fact]
    public void MarkTransferred_WhenActive_ShouldTransitionToTransferred()
    {
        var domain = CreateActiveDomain();

        domain.MarkTransferred();

        domain.Status.Should().Be(DomainStatus.Transferred);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenPendingRegistration_ShouldTransitionToCancelled()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), false, false);

        domain.Cancel();

        domain.Status.Should().Be(DomainStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenActive_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenTransferred_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();
        domain.MarkTransferred();

        var act = () => domain.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrowInvalidOperationException()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), false, false);
        domain.Cancel();

        var act = () => domain.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Renew ────────────────────────────────────────────────────────────────

    [Fact]
    public void Renew_WhenActive_ShouldExtendExpiresAtAndRaiseDomainRenewedEvent()
    {
        var domain = CreateActiveDomain();
        var newExpiry = DateTimeOffset.UtcNow.AddYears(2);

        domain.Renew(newExpiry);

        domain.ExpiresAt.Should().Be(newExpiry);
        domain.DomainEvents.OfType<DomainRenewedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Renew_WhenCancelled_ShouldThrowInvalidOperationException()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), false, false);
        domain.Cancel();

        var act = () => domain.Renew(DateTimeOffset.UtcNow.AddYears(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Renew_WhenTransferred_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();
        domain.MarkTransferred();

        var act = () => domain.Renew(DateTimeOffset.UtcNow.AddYears(2));

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Flag updates ─────────────────────────────────────────────────────────

    [Fact]
    public void SetAutoRenew_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetAutoRenew(false);

        domain.AutoRenew.Should().BeFalse();
    }

    [Fact]
    public void SetWhoisPrivacy_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetWhoisPrivacy(true);

        domain.WhoisPrivacy.Should().BeTrue();
    }

    [Fact]
    public void SetLock_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetLock(true);

        domain.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void SetEppCode_ShouldUpdateCode()
    {
        var domain = CreateActiveDomain();

        domain.SetEppCode("EPP-XYZ");

        domain.EppCode.Should().Be("EPP-XYZ");
    }

    // ── Nameservers ──────────────────────────────────────────────────────────

    [Fact]
    public void SetNameservers_ShouldReplaceExistingNameservers()
    {
        var domain = CreateActiveDomain();
        domain.SetNameservers(["ns1.old.com", "ns2.old.com"]);

        domain.SetNameservers(["ns1.new.com", "ns2.new.com", "ns3.new.com"]);

        domain.Nameservers.Should().HaveCount(3);
        domain.Nameservers.Select(n => n.Host).Should().Contain("ns1.new.com");
    }

    // ── DNS records ──────────────────────────────────────────────────────────

    [Fact]
    public void AddDnsRecord_ShouldAddRecordToCollection()
    {
        var domain = CreateActiveDomain();

        domain.AddDnsRecord(DnsRecordType.A, "www", "1.2.3.4", 3600, null);

        domain.DnsRecords.Should().HaveCount(1);
        domain.DnsRecords[0].Type.Should().Be(DnsRecordType.A);
        domain.DnsRecords[0].Host.Should().Be("www");
        domain.DnsRecords[0].Value.Should().Be("1.2.3.4");
    }

    [Fact]
    public void RemoveDnsRecord_ShouldRemoveRecordById()
    {
        var domain = CreateActiveDomain();
        domain.AddDnsRecord(DnsRecordType.TXT, "@", "v=spf1 include:example.com ~all", 300, null);
        var record = domain.DnsRecords[0];

        domain.RemoveDnsRecord(record.Id);

        domain.DnsRecords.Should().BeEmpty();
    }

    [Fact]
    public void RemoveDnsRecord_WhenNotFound_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.RemoveDnsRecord(999);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateDnsRecord_ShouldUpdateValueAndTtl()
    {
        var domain = CreateActiveDomain();
        domain.AddDnsRecord(DnsRecordType.A, "mail", "1.2.3.4", 3600, null);
        var record = domain.DnsRecords[0];

        domain.UpdateDnsRecord(record.Id, "5.6.7.8", 7200, null);

        domain.DnsRecords[0].Value.Should().Be("5.6.7.8");
        domain.DnsRecords[0].Ttl.Should().Be(7200);
    }

    [Fact]
    public void UpdateDnsRecord_WhenNotFound_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.UpdateDnsRecord(999, "value", 3600, null);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── LinkService ──────────────────────────────────────────────────────────

    [Fact]
    public void LinkService_ShouldSetLinkedServiceId()
    {
        var domain = CreateActiveDomain();

        domain.LinkService(42);

        domain.LinkedServiceId.Should().Be(42);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates an active domain for use in tests that require an Active status.</summary>
    /// <returns>An active <see cref="Domain"/> aggregate.</returns>
    private static Domain CreateActiveDomain()
    {
        var domain = Domain.Register(1, "example.com", DateTimeOffset.UtcNow.AddYears(1), true, false);
        domain.Activate("REG-TEST");
        domain.ClearDomainEvents();
        return domain;
    }
}
