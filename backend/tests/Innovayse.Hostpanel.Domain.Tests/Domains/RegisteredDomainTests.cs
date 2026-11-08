using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for the <see cref="RegisteredDomain"/> aggregate lifecycle.</summary>
public sealed class RegisteredDomainTests
{
    /// <summary>The ISO 4217 code a test domain is priced in when the test does not care which.</summary>
    private const string Currency = "USD";

    /// <summary>The fixed moment every test acts at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    // ── Register / Activate ──────────────────────────────────────────────────

    /// <summary>Register, should create domain with pending registration status.</summary>
    [Fact]
    public void Register_ShouldCreateDomainWithPendingRegistrationStatus()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), true, true, priceCurrency: Currency);

        domain.Status.Should().Be(DomainStatus.PendingRegistration);
        domain.ClientId.Should().Be(1);
        domain.Name.Should().Be("example.com");
        domain.Tld.Should().Be("com");
        domain.AutoRenew.Should().BeTrue();
        domain.WhoisPrivacy.Should().BeTrue();
        domain.RegisteredAt.Should().Be(Now);
    }

    /// <summary>
    /// Every factory stores the name trimmed and lower case, so the unique index on the name means
    /// one row per domain whatever case the caller typed.
    /// </summary>
    /// <param name="factory">Which factory built the domain.</param>
    [Theory]
    [InlineData("register")]
    [InlineData("import")]
    [InlineData("transfer")]
    public void Factories_NormaliseTheNameToLowerCase(string factory)
    {
        var domain = factory switch
        {
            "register" => RegisteredDomain.Register(1, " Example.COM ", Now, Now.AddYears(1), true, false, Currency),
            "import" => RegisteredDomain.Import(1, " Example.COM ", DomainStatus.Active, Now, Now.AddYears(1), true, Currency, null),
            _ => RegisteredDomain.CreateTransfer(1, " Example.COM ", Now, Currency),
        };

        domain.Name.Should().Be("example.com");
        domain.Tld.Should().Be("com");
    }

    /// <summary>
    /// Import recreates a domain in the status it had at the source, in its billing currency, with
    /// no registrar reference and no domain event, so importing never notifies or suspends anything.
    /// </summary>
    /// <param name="status">The source status the domain is imported in.</param>
    [Theory]
    [InlineData(DomainStatus.Active)]
    [InlineData(DomainStatus.Expired)]
    [InlineData(DomainStatus.Redemption)]
    [InlineData(DomainStatus.Transferred)]
    [InlineData(DomainStatus.Cancelled)]
    public void Import_SetsTheSourceStateWithoutEventsOrRegistrarRef(DomainStatus status)
    {
        var domain = RegisteredDomain.Import(1, "example.am", status, Now, Now.AddYears(1), true, "AMD", "source-registrar");

        domain.Status.Should().Be(status);
        domain.PriceCurrency.Should().Be("AMD");
        domain.Tld.Should().Be("am");
        domain.ExpiresAt.Should().Be(Now.AddYears(1));
        domain.NextDueDate.Should().Be(Now.AddYears(1));
        domain.Registrar.Should().Be("source-registrar");
        domain.RegistrarRef.Should().BeNull();
        domain.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// Every factory stores the registrar module it is given, and a domain built without one has none,
    /// so the free-text registrar of an import never decides where an operation goes.
    /// </summary>
    /// <param name="factory">Which factory built the domain.</param>
    [Theory]
    [InlineData("register")]
    [InlineData("import")]
    [InlineData("transfer")]
    public void Factories_StoreTheRegistrarModuleTheyAreGiven(string factory)
    {
        var domain = factory switch
        {
            "register" => RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), true, false, Currency, registrarModule: RegistrarModule.Namecheap),
            "import" => RegisteredDomain.Import(1, "example.com", DomainStatus.Active, Now, Now.AddYears(1), true, Currency, "Name.am", RegistrarModule.Namecheap),
            _ => RegisteredDomain.CreateTransfer(1, "example.com", Now, Currency, registrarModule: RegistrarModule.Namecheap),
        };
        var withoutModule = RegisteredDomain.Import(1, "other.com", DomainStatus.Active, Now, Now.AddYears(1), true, Currency, "Name.am");

        domain.RegistrarModule.Should().Be(RegistrarModule.Namecheap);
        withoutModule.RegistrarModule.Should().BeNull();
        withoutModule.Registrar.Should().Be("Name.am");
    }

    /// <summary>Register keeps the currency the price was paid in instead of assuming one.</summary>
    [Fact]
    public void Register_WithPriceCurrency_StoresIt()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), true, false, "AMD", 5000m, 5000m);

        domain.PriceCurrency.Should().Be("AMD");
    }

    /// <summary>Activate, after register, should transition to active and raise domain registered event.</summary>
    [Fact]
    public void Activate_AfterRegister_ShouldTransitionToActiveAndRaiseDomainRegisteredEvent()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), true, false, priceCurrency: Currency);

        domain.Activate("REG-001", domain.ExpiresAt, Now);

        domain.Status.Should().Be(DomainStatus.Active);
        domain.RegistrarRef.Should().Be("REG-001");
        domain.DomainEvents.Should().ContainSingle(e => e is DomainRegisteredEvent);
        var evt = (DomainRegisteredEvent)domain.DomainEvents.Single();
        evt.ClientId.Should().Be(1);
        evt.Name.Should().Be("example.com");
        evt.OccurredAt.Should().Be(Now);
    }

    /// <summary>Activate, when not pending registration, should throw invalid operation exception.</summary>
    [Fact]
    public void Activate_WhenNotPendingRegistration_ShouldThrowInvalidOperationException()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);
        domain.Activate("REG-001", domain.ExpiresAt, Now);

        var act = () => domain.Activate("REG-002", domain.ExpiresAt, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── CreateTransfer / ActivateTransfer ────────────────────────────────────

    /// <summary>Create transfer, should create domain with pending transfer status.</summary>
    [Fact]
    public void CreateTransfer_ShouldCreateDomainWithPendingTransferStatus()
    {
        var domain = RegisteredDomain.CreateTransfer(2, "transfer.net", Now, priceCurrency: Currency);

        domain.Status.Should().Be(DomainStatus.PendingTransfer);
        domain.ClientId.Should().Be(2);
        domain.Name.Should().Be("transfer.net");
        domain.Tld.Should().Be("net");
        domain.RegisteredAt.Should().Be(Now);
    }

    /// <summary>A transfer is billed in the currency it was created with.</summary>
    [Fact]
    public void CreateTransfer_KeepsThePriceCurrency()
    {
        var domain = RegisteredDomain.CreateTransfer(2, "transfer.net", Now, "AMD", 4600m, 4600m);

        domain.PriceCurrency.Should().Be("AMD");
        domain.FirstPaymentAmount.Should().Be(4600m);
    }

    /// <summary>A transfer without a currency is refused.</summary>
    /// <param name="currency">The blank currency code passed in.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateTransfer_WithoutCurrency_Throws(string currency)
    {
        var act = () => RegisteredDomain.CreateTransfer(2, "transfer.net", Now, currency);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A registration without a currency is refused.</summary>
    /// <param name="currency">The blank currency code passed in.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_WithoutCurrency_Throws(string currency)
    {
        var act = () => RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, currency);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>Activate transfer, should transition to active and raise domain transferred in event.</summary>
    [Fact]
    public void ActivateTransfer_ShouldTransitionToActiveAndRaiseDomainTransferredInEvent()
    {
        var expiresAt = Now.AddYears(2);
        var domain = RegisteredDomain.CreateTransfer(2, "transfer.net", Now, priceCurrency: Currency);

        domain.ActivateTransfer("REG-TR-001", expiresAt, Now);

        domain.Status.Should().Be(DomainStatus.Active);
        domain.RegistrarRef.Should().Be("REG-TR-001");
        domain.ExpiresAt.Should().Be(expiresAt);
        domain.DomainEvents.Should().ContainSingle(e => e is DomainTransferredInEvent);
        var evt = (DomainTransferredInEvent)domain.DomainEvents.Single();
        evt.ClientId.Should().Be(2);
        evt.Name.Should().Be("transfer.net");
        evt.OccurredAt.Should().Be(Now);
    }

    // ── MarkExpired ──────────────────────────────────────────────────────────

    /// <summary>Mark expired, when active, should transition to expired and raise domain expired event.</summary>
    [Fact]
    public void MarkExpired_WhenActive_ShouldTransitionToExpiredAndRaiseDomainExpiredEvent()
    {
        var domain = CreateActiveDomain();

        domain.MarkExpired(Now);

        domain.Status.Should().Be(DomainStatus.Expired);
        domain.DomainEvents.OfType<DomainExpiredEvent>().Should().ContainSingle().Which.OccurredAt.Should().Be(Now);
    }

    /// <summary>Mark expired, when not active, should throw invalid operation exception.</summary>
    [Fact]
    public void MarkExpired_WhenNotActive_ShouldThrowInvalidOperationException()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);

        var act = () => domain.MarkExpired(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── MarkRedemption ───────────────────────────────────────────────────────

    /// <summary>Mark redemption, when expired, should transition to redemption.</summary>
    [Fact]
    public void MarkRedemption_WhenExpired_ShouldTransitionToRedemption()
    {
        var domain = CreateActiveDomain();
        domain.MarkExpired(Now);

        domain.MarkRedemption();

        domain.Status.Should().Be(DomainStatus.Redemption);
    }

    /// <summary>Mark redemption, when not expired, should throw invalid operation exception.</summary>
    [Fact]
    public void MarkRedemption_WhenNotExpired_ShouldThrowInvalidOperationException()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.MarkRedemption();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── MarkTransferred ──────────────────────────────────────────────────────

    /// <summary>Mark transferred, when active, should transition to transferred.</summary>
    [Fact]
    public void MarkTransferred_WhenActive_ShouldTransitionToTransferred()
    {
        var domain = CreateActiveDomain();

        domain.MarkTransferred();

        domain.Status.Should().Be(DomainStatus.Transferred);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    /// <summary>Cancel, when pending registration, should transition to cancelled.</summary>
    [Fact]
    public void Cancel_WhenPendingRegistration_ShouldTransitionToCancelled()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);

        domain.Cancel();

        domain.Status.Should().Be(DomainStatus.Cancelled);
    }

    /// <summary>Cancel, when active, throws with domain cancel not allowed.</summary>
    [Fact]
    public void Cancel_WhenActive_ThrowsWithDomainCancelNotAllowed()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.Cancel();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainCancelNotAllowed);
    }

    /// <summary>Cancel, when transferred, throws with domain cancel not allowed.</summary>
    [Fact]
    public void Cancel_WhenTransferred_ThrowsWithDomainCancelNotAllowed()
    {
        var domain = CreateActiveDomain();
        domain.MarkTransferred();

        var act = () => domain.Cancel();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainCancelNotAllowed);
    }

    /// <summary>Cancel, when already cancelled, throws with domain cancel not allowed.</summary>
    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsWithDomainCancelNotAllowed()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);
        domain.Cancel();

        var act = () => domain.Cancel();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainCancelNotAllowed);
    }

    // ── Renew ────────────────────────────────────────────────────────────────

    /// <summary>Renew, when active, should extend expires at and raise domain renewed event.</summary>
    [Fact]
    public void Renew_WhenActive_ShouldExtendExpiresAtAndRaiseDomainRenewedEvent()
    {
        var domain = CreateActiveDomain();
        var newExpiry = Now.AddYears(2);

        domain.Renew(newExpiry, Now);

        domain.ExpiresAt.Should().Be(newExpiry);
        domain.DomainEvents.OfType<DomainRenewedEvent>().Should().ContainSingle().Which.OccurredAt.Should().Be(Now);
    }

    /// <summary>
    /// A renewal bought on an order line stores that line, so a retry of it can tell its own renewal; a renewal that is not a
    /// line's clears it, because the stored line is only ever the last renewal's.
    /// </summary>
    [Fact]
    public void Renew_StoresTheOrderLineOfTheLastRenewal()
    {
        var domain = CreateActiveDomain();

        domain.Renew(Now.AddYears(2), Now, orderItemId: 7);
        var afterLine = domain.LastRenewalOrderItemId;
        domain.Renew(Now.AddYears(3), Now);

        afterLine.Should().Be(7);
        domain.LastRenewalOrderItemId.Should().BeNull();
    }

    /// <summary>Renew, when cancelled, throws with domain renew not allowed.</summary>
    [Fact]
    public void Renew_WhenCancelled_ThrowsWithDomainRenewNotAllowed()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);
        domain.Cancel();

        var act = () => domain.Renew(Now.AddYears(2), Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainRenewNotAllowed);
    }

    /// <summary>Renew, when transferred, throws with domain renew not allowed.</summary>
    [Fact]
    public void Renew_WhenTransferred_ThrowsWithDomainRenewNotAllowed()
    {
        var domain = CreateActiveDomain();
        domain.MarkTransferred();

        var act = () => domain.Renew(Now.AddYears(2), Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainRenewNotAllowed);
    }

    // ── Flag updates ─────────────────────────────────────────────────────────

    /// <summary>Set auto renew, should update flag.</summary>
    [Fact]
    public void SetAutoRenew_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetAutoRenew(false);

        domain.AutoRenew.Should().BeFalse();
    }

    /// <summary>Set whois privacy, should update flag.</summary>
    [Fact]
    public void SetWhoisPrivacy_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetWhoisPrivacy(true);

        domain.WhoisPrivacy.Should().BeTrue();
    }

    /// <summary>Set lock, should update flag.</summary>
    [Fact]
    public void SetLock_ShouldUpdateFlag()
    {
        var domain = CreateActiveDomain();

        domain.SetLock(true);

        domain.IsLocked.Should().BeTrue();
    }

    /// <summary>Set epp code, should update code.</summary>
    [Fact]
    public void SetEppCode_ShouldUpdateCode()
    {
        var domain = CreateActiveDomain();

        domain.SetEppCode("EPP-XYZ");

        domain.EppCode.Should().Be("EPP-XYZ");
    }

    // ── Nameservers ──────────────────────────────────────────────────────────

    /// <summary>Set nameservers, should replace existing nameservers.</summary>
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

    /// <summary>Add dns record, should add record to collection.</summary>
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

    /// <summary>Remove dns record, should remove record by id.</summary>
    [Fact]
    public void RemoveDnsRecord_ShouldRemoveRecordById()
    {
        var domain = CreateActiveDomain();
        domain.AddDnsRecord(DnsRecordType.TXT, "@", "v=spf1 include:example.com ~all", 300, null);
        var record = domain.DnsRecords[0];

        domain.RemoveDnsRecord(record.Id);

        domain.DnsRecords.Should().BeEmpty();
    }

    /// <summary>Remove dns record, when not found, throws with domain dns record not found.</summary>
    [Fact]
    public void RemoveDnsRecord_WhenNotFound_ThrowsWithDomainDnsRecordNotFound()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.RemoveDnsRecord(999);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainDnsRecordNotFound);
    }

    /// <summary>Update dns record, should update value and ttl.</summary>
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

    /// <summary>Update dns record, when not found, throws with domain dns record not found.</summary>
    [Fact]
    public void UpdateDnsRecord_WhenNotFound_ThrowsWithDomainDnsRecordNotFound()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.UpdateDnsRecord(999, "value", 3600, null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainDnsRecordNotFound);
    }

    /// <summary>
    /// UpdateForwardingRule with an id that names no rule throws <see cref="DomainRuleException"/>
    /// with <c>DOMAIN_FORWARDING_RULE_NOT_FOUND</c>.
    /// </summary>
    [Fact]
    public void UpdateForwardingRule_WhenNotFound_ThrowsWithDomainForwardingRuleNotFound()
    {
        var domain = CreateActiveDomain();

        var act = () => domain.UpdateForwardingRule(999, "info", "dest@example.com", true);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainForwardingRuleNotFound);
    }

    // ── LinkService ──────────────────────────────────────────────────────────

    /// <summary>Link service, should set linked service id.</summary>
    [Fact]
    public void LinkService_ShouldSetLinkedServiceId()
    {
        var domain = CreateActiveDomain();

        domain.LinkService(42);

        domain.LinkedServiceId.Should().Be(42);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates an active domain for use in tests that require an Active status.</summary>
    /// <returns>An active <see cref="RegisteredDomain"/> aggregate.</returns>
    private static RegisteredDomain CreateActiveDomain()
    {
        var domain = RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), true, false, priceCurrency: Currency);
        domain.Activate("REG-TEST", domain.ExpiresAt, Now);
        domain.ClearDomainEvents();
        return domain;
    }

    /// <summary>Backdate moves the registration date and leaves the expiry alone.</summary>
    [Fact]
    public void Backdate_ShouldMoveTheRegistrationDateOnly()
    {
        var expiresAt = Now.AddMonths(3);
        var domain = RegisteredDomain.Register(1, "example.com", Now, expiresAt, true, false, priceCurrency: Currency);
        var registeredAt = new DateTimeOffset(2025, 12, 1, 9, 0, 0, TimeSpan.Zero);

        domain.Backdate(registeredAt);

        domain.RegisteredAt.Should().Be(registeredAt);
        domain.ExpiresAt.Should().Be(expiresAt);
    }

    /// <summary>Renew moves the billing due date with the new expiry.</summary>
    [Fact]
    public void Renew_MovesNextDueDateWithExpiry()
    {
        var domain = CreateActiveDomain();
        var renewedTo = Now.AddYears(3);

        domain.Renew(renewedTo, Now);

        domain.NextDueDate.Should().Be(renewedTo);
    }

    /// <summary>Renew brings an expired domain back to active when the new expiry lies ahead.</summary>
    [Fact]
    public void Renew_WhenExpired_ReturnsToActive()
    {
        var domain = CreateActiveDomain();
        domain.MarkExpired(Now);

        domain.Renew(Now.AddYears(1), Now);

        domain.Status.Should().Be(DomainStatus.Active);
    }

    /// <summary>Renew brings a domain in redemption back to active when the new expiry lies ahead.</summary>
    [Fact]
    public void Renew_WhenInRedemption_ReturnsToActive()
    {
        var domain = CreateActiveDomain();
        domain.MarkExpired(Now);
        domain.MarkRedemption();

        domain.Renew(Now.AddYears(1), Now);

        domain.Status.Should().Be(DomainStatus.Active);
    }

    /// <summary>Renew leaves an expired domain expired when the new expiry is still in the past.</summary>
    [Fact]
    public void Renew_WhenExpiredAndNewExpiryPast_StaysExpired()
    {
        var domain = CreateActiveDomain();
        domain.MarkExpired(Now);

        domain.Renew(Now.AddDays(-1), Now);

        domain.Status.Should().Be(DomainStatus.Expired);
    }

    /// <summary>Activate takes the registrar's expiry and moves the due date with it.</summary>
    [Fact]
    public void Activate_SetsExpiryAndNextDueDate()
    {
        var domain = NewDomain();
        var expiresAt = Now.AddYears(2);

        domain.Activate("REG-1", expiresAt, Now);

        domain.ExpiresAt.Should().Be(expiresAt);
        domain.NextDueDate.Should().Be(expiresAt);
    }

    /// <summary>ActivateTransfer sets the due date with the expiry, instead of leaving it at the minimum date.</summary>
    [Fact]
    public void ActivateTransfer_SetsNextDueDate()
    {
        var domain = RegisteredDomain.CreateTransfer(1, "transfer.test", Now, registrationPeriod: 2, priceCurrency: Currency);
        var expiresAt = Now.AddYears(2);

        domain.ActivateTransfer("TR-1", expiresAt, Now);

        domain.NextDueDate.Should().Be(expiresAt);
        domain.RegistrationPeriod.Should().Be(2);
    }

    /// <summary>LinkService links a service to an active domain.</summary>
    [Fact]
    public void LinkService_WhenActive_Links()
    {
        var domain = CreateActiveDomain();

        domain.LinkService(42);

        domain.LinkedServiceId.Should().Be(42);
    }

    /// <summary>LinkService refuses a cancelled domain with the link-not-allowed code.</summary>
    [Fact]
    public void LinkService_WhenCancelled_ThrowsWithDomainLinkNotAllowed()
    {
        var domain = NewDomain();
        domain.Cancel();

        var act = () => domain.LinkService(42);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainLinkNotAllowed);
    }

    /// <summary>LinkService refuses a domain transferred away with the link-not-allowed code.</summary>
    [Fact]
    public void LinkService_WhenTransferred_ThrowsWithDomainLinkNotAllowed()
    {
        var domain = CreateActiveDomain();
        domain.MarkTransferred();

        var act = () => domain.LinkService(42);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainLinkNotAllowed);
    }

    /// <summary>Builds a freshly registered domain the tests act on.</summary>
    /// <returns>A domain in pending registration.</returns>
    private static RegisteredDomain NewDomain() =>
        RegisteredDomain.Register(1, "example.com", Now, Now.AddYears(1), false, false, priceCurrency: Currency);

    /// <summary>SetDnsManagement switches the flag on and back off.</summary>
    [Fact]
    public void SetDnsManagement_TogglesTheFlag()
    {
        var domain = NewDomain();

        domain.SetDnsManagement(true);
        domain.DnsManagement.Should().BeTrue();

        domain.SetDnsManagement(false);
        domain.DnsManagement.Should().BeFalse();
    }

    /// <summary>SetEmailForwarding switches the flag on and back off.</summary>
    [Fact]
    public void SetEmailForwarding_TogglesTheFlag()
    {
        var domain = NewDomain();

        domain.SetEmailForwarding(true);
        domain.EmailForwarding.Should().BeTrue();

        domain.SetEmailForwarding(false);
        domain.EmailForwarding.Should().BeFalse();
    }

    /// <summary>AddForwardingRule appends a rule carrying the source and destination.</summary>
    [Fact]
    public void AddForwardingRule_AppendsTheRule()
    {
        var domain = NewDomain();

        domain.AddForwardingRule("info", "owner@example.test");

        var rule = domain.EmailForwardingRules.Should().ContainSingle().Subject;
        rule.Source.Should().Be("info");
        rule.Destination.Should().Be("owner@example.test");
    }

    /// <summary>RemoveForwardingRule drops the rule with the given id.</summary>
    [Fact]
    public void RemoveForwardingRule_KnownId_RemovesTheRule()
    {
        var domain = NewDomain();
        domain.AddForwardingRule("info", "owner@example.test");
        var ruleId = domain.EmailForwardingRules.Single().Id;

        domain.RemoveForwardingRule(ruleId);

        domain.EmailForwardingRules.Should().BeEmpty();
    }

    /// <summary>RemoveForwardingRule refuses an id the domain does not carry, with the not-found code.</summary>
    [Fact]
    public void RemoveForwardingRule_UnknownId_ThrowsWithNotFoundCode()
    {
        var domain = NewDomain();

        var act = () => domain.RemoveForwardingRule(12345);

        act.Should().Throw<DomainRuleException>()
            .Which.Code.Should().Be(DomainsErrors.DomainForwardingRuleNotFound);
    }

    /// <summary>AddReminder records which reminder was sent and to whom.</summary>
    [Fact]
    public void AddReminder_RecordsTheReminder()
    {
        var domain = NewDomain();

        var sentAt = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        domain.AddReminder(DomainReminderType.ThirtyDaysBeforeExpiry, "owner@example.test", sentAt);

        var reminder = domain.Reminders.Should().ContainSingle().Subject;
        reminder.ReminderType.Should().Be(DomainReminderType.ThirtyDaysBeforeExpiry);
        reminder.SentTo.Should().Be("owner@example.test");
        reminder.SentAt.Should().Be(sentAt);
    }

    /// <summary>RemindOfExpiry records the nearest warning due and raises the expiring event.</summary>
    [Fact]
    public void RemindOfExpiry_WithinWindow_RecordsAndRaisesEvent()
    {
        var domain = ActiveDomainExpiringIn(TimeSpan.FromDays(10));

        var raised = domain.RemindOfExpiry("client-1", Now);

        raised.Should().BeTrue();
        domain.Reminders.Should().ContainSingle().Which.ReminderType.Should().Be(DomainReminderType.FourteenDaysBeforeExpiry);
        domain.DomainEvents.OfType<DomainExpiringEvent>().Should().ContainSingle().Which.OccurredAt.Should().Be(Now);
    }

    /// <summary>A domain added late gets only the nearest warning, not every larger one on the following days.</summary>
    [Fact]
    public void RemindOfExpiry_AddedLate_SendsOnlyTheNearestThreshold()
    {
        var domain = ActiveDomainExpiringIn(TimeSpan.FromDays(5));

        domain.RemindOfExpiry("client-1", Now).Should().BeTrue();
        domain.RemindOfExpiry("client-1", Now.AddDays(1)).Should().BeFalse();
        domain.RemindOfExpiry("client-1", Now.AddDays(2)).Should().BeFalse();

        domain.Reminders.Should().ContainSingle().Which.ReminderType.Should().Be(DomainReminderType.SevenDaysBeforeExpiry);
    }

    /// <summary>RemindOfExpiry sends nothing while the expiry lies beyond the warning window.</summary>
    [Fact]
    public void RemindOfExpiry_BeyondWindow_SendsNothing()
    {
        var domain = ActiveDomainExpiringIn(TimeSpan.FromDays(RegisteredDomain.ExpiryWarningWindowDays + 5));

        domain.RemindOfExpiry("client-1", Now).Should().BeFalse();

        domain.Reminders.Should().BeEmpty();
    }

    /// <summary>After a renewal the warnings of the new expiry cycle go out again.</summary>
    [Fact]
    public void RemindOfExpiry_AfterRenewal_RemindsAgain()
    {
        var domain = ActiveDomainExpiringIn(TimeSpan.FromDays(20));
        domain.RemindOfExpiry("client-1", Now).Should().BeTrue();
        domain.Renew(domain.ExpiresAt.AddYears(1), Now);

        var nextCycle = domain.ExpiresAt.AddDays(-20);

        domain.RemindOfExpiry("client-1", nextCycle).Should().BeTrue();
        domain.Reminders.Should().HaveCount(2)
            .And.OnlyContain(r => r.ReminderType == DomainReminderType.ThirtyDaysBeforeExpiry);
    }

    /// <summary>Builds an active domain expiring the given time after <see cref="Now"/>.</summary>
    /// <param name="left">Time left until expiry.</param>
    /// <returns>An active domain.</returns>
    private static RegisteredDomain ActiveDomainExpiringIn(TimeSpan left)
    {
        var domain = RegisteredDomain.Register(1, "remind.test", Now, Now.Add(left), false, false, priceCurrency: Currency);
        domain.Activate("REG-R", Now.Add(left), Now);
        return domain;
    }
}
