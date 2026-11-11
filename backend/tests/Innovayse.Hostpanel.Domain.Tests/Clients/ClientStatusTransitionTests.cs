using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Clients.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>
/// Every move of the <see cref="Client"/> status matrix: Suspend, Close, Activate and Deactivate from
/// each of Active, Inactive, Suspended and Closed, with the domain event each successful move raises.
/// </summary>
public class ClientStatusTransitionTests
{
    /// <summary>The moment the creation is stamped with.</summary>
    private static readonly DateTimeOffset Created = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The moment the status change under test is stamped with.</summary>
    private static readonly DateTimeOffset Changed = new(2026, 3, 11, 8, 30, 0, TimeSpan.Zero);

    /// <summary>Builds a client that stands in <paramref name="status"/> with no domain event pending.</summary>
    /// <param name="status">The status the client starts the test in.</param>
    /// <returns>The client.</returns>
    private static Client InStatus(ClientStatus status)
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com", Created);
        switch (status)
        {
            case ClientStatus.Inactive:
                client.Deactivate(Created);
                break;
            case ClientStatus.Suspended:
                client.Suspend(Created);
                break;
            case ClientStatus.Closed:
                client.Close(Created);
                break;
        }

        client.ClearDomainEvents();
        return client;
    }

    /// <summary>Suspend is allowed from Active and Inactive and raises <see cref="ClientSuspendedEvent"/>.</summary>
    /// <param name="from">The status the client starts in.</param>
    [Theory]
    [InlineData(ClientStatus.Active)]
    [InlineData(ClientStatus.Inactive)]
    public void Suspend_FromActiveOrInactive_SuspendsAndRaisesTheEvent(ClientStatus from)
    {
        var client = InStatus(from);

        client.Suspend(Changed);

        Assert.Equal(ClientStatus.Suspended, client.Status);
        var raised = Assert.IsType<ClientSuspendedEvent>(Assert.Single(client.DomainEvents));
        Assert.Equal(Changed, raised.OccurredAt);
    }

    /// <summary>Suspend is refused from Suspended and Closed with <c>CLIENT_SUSPEND_NOT_ALLOWED</c>.</summary>
    /// <param name="from">The status the client starts in.</param>
    [Theory]
    [InlineData(ClientStatus.Suspended)]
    [InlineData(ClientStatus.Closed)]
    public void Suspend_FromSuspendedOrClosed_IsRefused(ClientStatus from)
    {
        var client = InStatus(from);

        var ex = Assert.Throws<DomainRuleException>(() => client.Suspend(Changed));

        Assert.Equal(ClientsErrors.ClientSuspendNotAllowed, ex.Code);
        Assert.Equal(from, client.Status);
        Assert.Empty(client.DomainEvents);
    }

    /// <summary>Close is allowed from Active, Inactive and Suspended and raises <see cref="ClientClosedEvent"/>.</summary>
    /// <param name="from">The status the client starts in.</param>
    [Theory]
    [InlineData(ClientStatus.Active)]
    [InlineData(ClientStatus.Inactive)]
    [InlineData(ClientStatus.Suspended)]
    public void Close_FromAnyOpenStatus_ClosesAndRaisesTheEvent(ClientStatus from)
    {
        var client = InStatus(from);

        client.Close(Changed);

        Assert.Equal(ClientStatus.Closed, client.Status);
        var raised = Assert.IsType<ClientClosedEvent>(Assert.Single(client.DomainEvents));
        Assert.Equal(Changed, raised.OccurredAt);
        Assert.Equal("user-1", raised.UserId);
    }

    /// <summary>Close is refused from Closed with <c>CLIENT_CLOSE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Close_FromClosed_IsRefused()
    {
        var client = InStatus(ClientStatus.Closed);

        var ex = Assert.Throws<DomainRuleException>(() => client.Close(Changed));

        Assert.Equal(ClientsErrors.ClientCloseNotAllowed, ex.Code);
        Assert.Empty(client.DomainEvents);
    }

    /// <summary>Activate is allowed from Inactive and Suspended and raises <see cref="ClientActivatedEvent"/>.</summary>
    /// <param name="from">The status the client starts in.</param>
    [Theory]
    [InlineData(ClientStatus.Inactive)]
    [InlineData(ClientStatus.Suspended)]
    public void Activate_FromInactiveOrSuspended_ActivatesAndRaisesTheEvent(ClientStatus from)
    {
        var client = InStatus(from);

        client.Activate(Changed);

        Assert.Equal(ClientStatus.Active, client.Status);
        var raised = Assert.IsType<ClientActivatedEvent>(Assert.Single(client.DomainEvents));
        Assert.Equal(Changed, raised.OccurredAt);
    }

    /// <summary>Activating an active account changes nothing and raises nothing.</summary>
    [Fact]
    public void Activate_FromActive_IsANoOp()
    {
        var client = InStatus(ClientStatus.Active);

        client.Activate(Changed);

        Assert.Equal(ClientStatus.Active, client.Status);
        Assert.Empty(client.DomainEvents);
    }

    /// <summary>Activate is refused from Closed with <c>CLIENT_ACTIVATE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Activate_FromClosed_IsRefused()
    {
        var client = InStatus(ClientStatus.Closed);

        var ex = Assert.Throws<DomainRuleException>(() => client.Activate(Changed));

        Assert.Equal(ClientsErrors.ClientActivateNotAllowed, ex.Code);
        Assert.Empty(client.DomainEvents);
    }

    /// <summary>Deactivate is allowed from Active and raises <see cref="ClientDeactivatedEvent"/>.</summary>
    [Fact]
    public void Deactivate_FromActive_DeactivatesAndRaisesTheEvent()
    {
        var client = InStatus(ClientStatus.Active);

        client.Deactivate(Changed);

        Assert.Equal(ClientStatus.Inactive, client.Status);
        var raised = Assert.IsType<ClientDeactivatedEvent>(Assert.Single(client.DomainEvents));
        Assert.Equal(Changed, raised.OccurredAt);
    }

    /// <summary>Deactivating an inactive account changes nothing and raises nothing.</summary>
    [Fact]
    public void Deactivate_FromInactive_IsANoOp()
    {
        var client = InStatus(ClientStatus.Inactive);

        client.Deactivate(Changed);

        Assert.Equal(ClientStatus.Inactive, client.Status);
        Assert.Empty(client.DomainEvents);
    }

    /// <summary>Deactivate is refused from Suspended and Closed with its own code, <c>CLIENT_DEACTIVATE_NOT_ALLOWED</c>.</summary>
    /// <param name="from">The status the client starts in.</param>
    [Theory]
    [InlineData(ClientStatus.Suspended)]
    [InlineData(ClientStatus.Closed)]
    public void Deactivate_FromSuspendedOrClosed_IsRefused(ClientStatus from)
    {
        var client = InStatus(from);

        var ex = Assert.Throws<DomainRuleException>(() => client.Deactivate(Changed));

        Assert.Equal(ClientsErrors.ClientDeactivateNotAllowed, ex.Code);
        Assert.Equal(from, client.Status);
        Assert.Empty(client.DomainEvents);
    }
}
