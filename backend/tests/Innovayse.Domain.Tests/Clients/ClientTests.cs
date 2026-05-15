namespace Innovayse.Domain.Tests.Clients;

using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Events;

/// <summary>Unit tests for the <see cref="Client"/> aggregate.</summary>
public class ClientTests
{
    /// <summary>Create should produce Active client with a domain event.</summary>
    [Fact]
    public void Create_ShouldReturnActiveClient_WithDomainEvent()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");

        Assert.Equal("user-1", client.UserId);
        Assert.Equal("John", client.FirstName);
        Assert.Equal(ClientStatus.Active, client.Status);
        Assert.Single(client.DomainEvents);
        Assert.IsType<ClientCreatedEvent>(client.DomainEvents[0]);
    }

    /// <summary>Update should change profile fields.</summary>
    [Fact]
    public void Update_ShouldChangeProfileFields()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Update("Jane", "Smith", "Acme Corp", "+1-555-0100");

        Assert.Equal("Jane", client.FirstName);
        Assert.Equal("Smith", client.LastName);
        Assert.Equal("Acme Corp", client.CompanyName);
        Assert.Equal("+1-555-0100", client.Phone);
    }

    /// <summary>Suspend should change status to Suspended.</summary>
    [Fact]
    public void Suspend_ShouldSetStatusToSuspended()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();

        Assert.Equal(ClientStatus.Suspended, client.Status);
    }

    /// <summary>Suspending an already suspended client should throw.</summary>
    [Fact]
    public void Suspend_WhenAlreadySuspended_ShouldThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();

        Assert.Throws<InvalidOperationException>(() => client.Suspend());
    }

    /// <summary>Activate should restore a suspended client to Active.</summary>
    [Fact]
    public void Activate_WhenSuspended_ShouldSetStatusToActive()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Suspend();
        client.Activate();

        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>Activating an already active account should not throw (idempotent).</summary>
    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.Activate(); // already Active — should be a no-op
        Assert.Equal(ClientStatus.Active, client.Status);
    }

    /// <summary>AddContact should append a contact to the collection.</summary>
    [Fact]
    public void AddContact_ShouldAppendToContacts()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.AddContact(
            "Jane", "Doe", null, "jane@example.com", null, ContactType.Billing,
            null, null, null, null, null, null,
            true, true, true, true, true, true);

        Assert.Single(client.Contacts);
        Assert.Equal("Jane", client.Contacts[0].FirstName);
        Assert.Equal("Doe", client.Contacts[0].LastName);
    }

    /// <summary>RemoveContact should remove the specified contact.</summary>
    [Fact]
    public void RemoveContact_ShouldRemoveFromContacts()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.AddContact(
            "Jane", "Doe", null, "jane@example.com", null, ContactType.Billing,
            null, null, null, null, null, null,
            true, true, true, true, true, true);

        // Contact Id will be 0 since EF Core hasn't assigned a real Id.
        var contactId = client.Contacts[0].Id;
        client.RemoveContact(contactId);

        Assert.Empty(client.Contacts);
    }

    /// <summary>RemoveContact with invalid ID should throw.</summary>
    [Fact]
    public void RemoveContact_WhenNotFound_ShouldThrow()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        Assert.Throws<InvalidOperationException>(() => client.RemoveContact(999));
    }

    /// <summary>UpdateContact should update the specified contact's fields.</summary>
    [Fact]
    public void UpdateContact_ShouldUpdateFields()
    {
        var client = Client.Create("user-1", "John", "Doe", "john@example.com");
        client.AddContact(
            "Jane", "Doe", null, "jane@example.com", null, ContactType.Billing,
            null, null, null, null, null, null,
            true, true, true, true, true, true);

        var contactId = client.Contacts[0].Id;
        client.UpdateContact(
            contactId, "Janet", "Smith", "Acme", "janet@example.com", "+1-555-0100", ContactType.Technical,
            "123 Main St", null, "Yerevan", null, "0010", "AM",
            false, true, false, true, false, true);

        var contact = client.Contacts[0];
        Assert.Equal("Janet", contact.FirstName);
        Assert.Equal("Smith", contact.LastName);
        Assert.Equal("Acme", contact.CompanyName);
        Assert.Equal("janet@example.com", contact.Email);
        Assert.Equal(ContactType.Technical, contact.Type);
        Assert.Equal("AM", contact.Country);
        Assert.False(contact.NotifyGeneral);
    }
}
