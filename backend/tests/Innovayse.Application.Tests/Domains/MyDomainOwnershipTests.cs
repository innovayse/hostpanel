namespace Innovayse.Application.Tests.Domains;

using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.AddMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.AddMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.DeleteMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.InitiateMyOutgoingTransfer;
using Innovayse.Application.Domains.Commands.ModifyMyDomainContact;
using Innovayse.Application.Domains.Commands.RenewDomain;
using Innovayse.Application.Domains.Commands.RenewMyDomain;
using Innovayse.Application.Domains.Commands.SetMyDomainAutoRenew;
using Innovayse.Application.Domains.Commands.SetMyDomainDnsManagement;
using Innovayse.Application.Domains.Commands.SetMyDomainEmailForwarding;
using Innovayse.Application.Domains.Commands.SetMyDomainRegistrarLock;
using Innovayse.Application.Domains.Commands.SetMyDomainWhoisPrivacy;
using Innovayse.Application.Domains.Commands.UpdateMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.UpdateMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.UpdateMyDomainNameservers;
using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetDomain;
using Innovayse.Application.Domains.Queries.GetMyDomain;
using Innovayse.Application.Domains.Queries.GetMyDomainNameservers;
using Innovayse.Application.Domains.Queries.GetMyDomainWhois;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains;
using Innovayse.Domain.Domains.Interfaces;
using Moq;
using Wolverine;
using Xunit;
using DomainEntity = Innovayse.Domain.Domains.Domain;

/// <summary>
/// Proves the client-facing domain routes are scoped to the caller, and that the scoping is a
/// property of the message rather than of the endpoint.
/// <para>
/// Before this, <c>MyDomainsController</c> ran the ownership check itself, eighteen times, ahead
/// of the dispatch. The check was real, but every command behind those actions --
/// <c>SetAutoRenewCommand</c>, <c>RenewDomainCommand</c>, <c>AddDnsRecordCommand</c> and the rest
/// -- is also dispatched by the admin <c>DomainsController</c>, and <c>RenewDomainCommand</c> by
/// the auto-renew job as well, so the guarantee held only for callers who happened to arrive
/// through that one endpoint.
/// </para>
/// <para>
/// Each test below asserts the same three things for one hole: a non-owner is refused, the
/// refusal is indistinguishable from "no such domain", and the shared use case is never
/// dispatched on refusal -- so nothing is loaded or written on a stranger's behalf and then
/// filtered afterwards.
/// </para>
/// </summary>
public sealed class MyDomainOwnershipTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>A client id that is deliberately not the caller's, so ownership must not match.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>The domain id every probe asks for.</summary>
    private const int DomainId = 7;

    /// <summary>The DNS record id the record-level probes name.</summary>
    private const int RecordId = 11;

    /// <summary>The email forwarding rule id the rule-level probes name.</summary>
    private const int RuleId = 13;

    /// <summary>Builds the caller's own client record. Its <c>Id</c> is 0, the unsaved default.</summary>
    /// <returns>A client owned by <see cref="CallerSubject"/>.</returns>
    private static Client CallerClient() => Client.Create(CallerSubject, "Jane", "Doe", "jane@example.com");

    /// <summary>Builds a domain belonging to the given client.</summary>
    /// <param name="clientId">FK to the owning client.</param>
    /// <returns>A domain in the pending-transfer state; its lifecycle is irrelevant here.</returns>
    private static DomainEntity DomainOf(int clientId) => DomainEntity.CreateTransfer(clientId, "example.com");

    /// <summary>Registrant details a stranger would try to overwrite a domain's contact with.</summary>
    /// <returns>A contact value object; none of its fields matter to these tests.</returns>
    private static DomainContact StrangersContact() => new(
        "Mallory", "Stranger", null, "mallory@evil.test", "+100000000",
        "1 Nowhere St", null, "Nowhere", "NA", "00000", "AA");

    /// <summary>
    /// Builds a <see cref="DomainOwnership"/> over the given world.
    /// </summary>
    /// <param name="client">What the client repository answers for the caller's subject.</param>
    /// <param name="domain">What the domain repository answers for <see cref="DomainId"/>.</param>
    /// <returns>The rule under test.</returns>
    private static IDomainOwnership OwnershipOver(Client? client, DomainEntity? domain)
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(CallerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var domains = new Mock<IDomainRepository>();
        domains.Setup(r => r.FindByIdAsync(DomainId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domain);

        var caller = new Mock<ICurrentRequestContext>();
        caller.Setup(c => c.RequireUserId()).Returns(CallerSubject);

        return new DomainOwnership(domains.Object, clients.Object, caller.Object);
    }

    /// <summary>An ownership rule that refuses everything, for the handler tests.</summary>
    /// <returns>A rule that throws <see cref="DomainNotFoundException"/>.</returns>
    private static IDomainOwnership RefusingOwnership() =>
        OwnershipOver(CallerClient(), DomainOf(StrangersClientId));

    /// <summary>An ownership rule that accepts, for the handler tests.</summary>
    /// <returns>A rule that completes.</returns>
    private static IDomainOwnership AcceptingOwnership() =>
        OwnershipOver(CallerClient(), DomainOf(clientId: 0));

    /// <summary>
    /// Asserts that no message of any kind left the bus. The value-returning handlers dispatch
    /// through the generic overload, so both are checked rather than only the one that use case
    /// happens to reach on the success path.
    /// </summary>
    /// <param name="bus">The bus the handler was given.</param>
    private static void AssertNothingDispatched(Mock<IMessageBus> bus)
    {
        bus.Verify(
            b => b.InvokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
        bus.Verify(
            b => b.InvokeAsync<string>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
        bus.Verify(
            b => b.InvokeAsync<DomainDto>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>
    /// The whole point: a caller who does not own the domain is refused rather than served.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenDomainBelongsToAnotherClient_RefusesAsync()
    {
        var ownership = OwnershipOver(CallerClient(), DomainOf(StrangersClientId));

        await Assert.ThrowsAsync<DomainNotFoundException>(
            () => ownership.RequireOwnedByCallerAsync(DomainId, CancellationToken.None));
    }

    /// <summary>
    /// A domain that does not exist answers exactly as a domain that is somebody else's -- same
    /// exception type, same sentence. Anything else would let the route be walked to find out
    /// which of the sequential ids are real.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenDomainDoesNotExist_AnswersAsForAStrangersDomainAsync()
    {
        var missing = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => OwnershipOver(CallerClient(), domain: null)
                .RequireOwnedByCallerAsync(DomainId, CancellationToken.None));

        var strangers = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => OwnershipOver(CallerClient(), DomainOf(StrangersClientId))
                .RequireOwnedByCallerAsync(DomainId, CancellationToken.None));

        Assert.Equal(strangers.Message, missing.Message);
        Assert.Equal(DomainNotFoundException.PublicMessage, missing.Message);
    }

    /// <summary>
    /// A caller with no client record is the third case that must not be distinguishable: an
    /// authenticated staff identity probing the client portal learns nothing about domain ids.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenCallerHasNoClientRecord_RefusesAlikeAsync()
    {
        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => OwnershipOver(client: null, DomainOf(StrangersClientId))
                .RequireOwnedByCallerAsync(DomainId, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
    }

    /// <summary>The rule must not refuse the caller their own domain.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenDomainIsTheCallersOwn_AllowsAsync()
    {
        await OwnershipOver(CallerClient(), DomainOf(clientId: 0))
            .RequireOwnedByCallerAsync(DomainId, CancellationToken.None);
    }

    /// <summary>
    /// Turning auto-renew on for another customer's domain is refused, and refused before the
    /// shared use case runs -- so the refusal is not a filter applied to work already done on a
    /// stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task SetMyDomainAutoRenewHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new SetMyDomainAutoRenewHandler(RefusingOwnership(), bus.Object);

        var message = new SetMyDomainAutoRenewCommand(DomainId, Value: true);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Changing another customer's WHOIS privacy is refused, and refused before the shared use case
    /// runs -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task SetMyDomainWhoisPrivacyHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new SetMyDomainWhoisPrivacyHandler(RefusingOwnership(), bus.Object);

        var message = new SetMyDomainWhoisPrivacyCommand(DomainId, Value: true);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Unlocking another customer's domain for transfer is refused, and refused before the shared
    /// use case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task SetMyDomainRegistrarLockHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new SetMyDomainRegistrarLockHandler(RefusingOwnership(), bus.Object);

        var message = new SetMyDomainRegistrarLockCommand(DomainId, Value: false);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Repointing another customer's nameservers is refused, and refused before the shared use case
    /// runs -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task UpdateMyDomainNameserversHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new UpdateMyDomainNameserversHandler(RefusingOwnership(), bus.Object);

        var message = new UpdateMyDomainNameserversCommand(DomainId, new[] { "ns1.evil.test", "ns2.evil.test" });

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Rewriting another customer's registrant contact is refused, and refused before the shared
    /// use case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task ModifyMyDomainContactHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new ModifyMyDomainContactHandler(RefusingOwnership(), bus.Object);

        var message = new ModifyMyDomainContactCommand(DomainId, StrangersContact());

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Pulling another customer's EPP authorization code is refused, and refused before the shared
    /// use case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task InitiateMyOutgoingTransferHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new InitiateMyOutgoingTransferHandler(RefusingOwnership(), bus.Object);

        var message = new InitiateMyOutgoingTransferCommand(DomainId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Spending another customer's money on a renewal is refused, and refused before the shared use
    /// case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RenewMyDomainHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new RenewMyDomainHandler(RefusingOwnership(), bus.Object);

        var message = new RenewMyDomainCommand(DomainId, Years: 1);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Writing into another customer's zone is refused, and refused before the shared use case runs
    /// -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task AddMyDomainDnsRecordHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new AddMyDomainDnsRecordHandler(RefusingOwnership(), bus.Object);

        var message = new AddMyDomainDnsRecordCommand(DomainId, DnsRecordType.A, "@", "203.0.113.1", 3600, null);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Rewriting an entry in another customer's zone is refused, and refused before the shared use
    /// case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task UpdateMyDomainDnsRecordHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new UpdateMyDomainDnsRecordHandler(RefusingOwnership(), bus.Object);

        var message = new UpdateMyDomainDnsRecordCommand(DomainId, RecordId, "203.0.113.1", 3600, null);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Deleting an entry from another customer's zone is refused, and refused before the shared use
    /// case runs -- so the refusal is not a filter applied to work already done on a stranger's
    /// behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task DeleteMyDomainDnsRecordHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new DeleteMyDomainDnsRecordHandler(RefusingOwnership(), bus.Object);

        var message = new DeleteMyDomainDnsRecordCommand(DomainId, RecordId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Toggling DNS management on another customer's domain is refused, and refused before the
    /// shared use case runs -- so the refusal is not a filter applied to work already done on a
    /// stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task SetMyDomainDnsManagementHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new SetMyDomainDnsManagementHandler(RefusingOwnership(), bus.Object);

        var message = new SetMyDomainDnsManagementCommand(DomainId, Enabled: true);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Toggling email forwarding on another customer's domain is refused, and refused before the
    /// shared use case runs -- so the refusal is not a filter applied to work already done on a
    /// stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task SetMyDomainEmailForwardingHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new SetMyDomainEmailForwardingHandler(RefusingOwnership(), bus.Object);

        var message = new SetMyDomainEmailForwardingCommand(DomainId, Enabled: true);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Redirecting another customer's mail is refused, and refused before the shared use case runs
    /// -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task AddMyDomainEmailForwardingRuleHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new AddMyDomainEmailForwardingRuleHandler(RefusingOwnership(), bus.Object);

        var message = new AddMyDomainEmailForwardingRuleCommand(DomainId, "info", "mallory@evil.test");

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Repointing another customer's mail rule is refused, and refused before the shared use case
    /// runs -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task UpdateMyDomainEmailForwardingRuleHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new UpdateMyDomainEmailForwardingRuleHandler(RefusingOwnership(), bus.Object);

        var message = new UpdateMyDomainEmailForwardingRuleCommand(DomainId, RuleId, "x", "m@evil.test", true);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Deleting another customer's mail rule is refused, and refused before the shared use case
    /// runs -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task DeleteMyDomainEmailForwardingRuleHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new DeleteMyDomainEmailForwardingRuleHandler(RefusingOwnership(), bus.Object);

        var message = new DeleteMyDomainEmailForwardingRuleCommand(DomainId, RuleId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Reading another customer's domain is refused, and refused before the shared use case runs --
    /// so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyDomainHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new GetMyDomainHandler(RefusingOwnership(), bus.Object);

        var message = new GetMyDomainQuery(DomainId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Reading another customer's nameservers is refused, and refused before the shared use case
    /// runs -- so the refusal is not a filter applied to work already done on a stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyDomainNameserversHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new GetMyDomainNameserversHandler(RefusingOwnership(), bus.Object);

        var message = new GetMyDomainNameserversQuery(DomainId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// Running a WHOIS lookup on another customer's domain is refused, and refused before the
    /// shared use case runs -- so the refusal is not a filter applied to work already done on a
    /// stranger's behalf.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyDomainWhoisHandler_WhenNotTheCallersOwn_RefusesWithoutDispatchingAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new GetMyDomainWhoisHandler(RefusingOwnership(), bus.Object);

        var message = new GetMyDomainWhoisQuery(DomainId);

        var refusal = await Assert.ThrowsAsync<DomainNotFoundException>(
            () => handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(DomainNotFoundException.PublicMessage, refusal.Message);
        AssertNothingDispatched(bus);
    }

    /// <summary>
    /// The wrapper is not a no-op: on the caller's own domain the shared command is dispatched
    /// with the route id intact. Without this, a handler that refused everything would pass every
    /// refusal test above.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RenewMyDomainHandler_WhenDomainIsTheCallersOwn_DispatchesTheSharedCommandAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new RenewMyDomainHandler(AcceptingOwnership(), bus.Object);

        await handler.HandleAsync(new RenewMyDomainCommand(DomainId, Years: 2), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync(
                It.Is<RenewDomainCommand>(c => c.DomainId == DomainId && c.Years == 2),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    /// <summary>
    /// The same for a read: on the caller's own domain the shared projection is reached, so the
    /// client route keeps answering with the mapping the admin route already maintains.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyDomainHandler_WhenDomainIsTheCallersOwn_DispatchesTheSharedReadAsync()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<DomainDto>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((DomainDto)null!);

        var handler = new GetMyDomainHandler(AcceptingOwnership(), bus.Object);

        await handler.HandleAsync(new GetMyDomainQuery(DomainId), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync<DomainDto>(
                It.Is<GetDomainQuery>(q => q.DomainId == DomainId),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }
}
