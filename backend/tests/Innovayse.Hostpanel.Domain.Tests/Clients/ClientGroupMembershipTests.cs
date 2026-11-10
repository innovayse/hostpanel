using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>Unit tests for the client group a <see cref="Client"/> belongs to: assigned, replaced and cleared.</summary>
public class ClientGroupMembershipTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A new client belongs to no group.</summary>
    [Fact]
    public void Create_HasNoGroup()
    {
        NewClient().ClientGroupId.Should().BeNull();
    }

    /// <summary>Assigning stores the group id, and assigning another replaces it.</summary>
    [Fact]
    public void AssignToGroup_StoresAndReplacesTheGroup()
    {
        var client = NewClient();

        client.AssignToGroup(3);
        client.ClientGroupId.Should().Be(3);

        client.AssignToGroup(7);
        client.ClientGroupId.Should().Be(7);
    }

    /// <summary>Clearing takes the client out of its group, and clearing a client with none changes nothing.</summary>
    [Fact]
    public void ClearGroup_TakesTheClientOutOfItsGroup()
    {
        var client = NewClient();
        client.AssignToGroup(3);

        client.ClearGroup();
        client.ClearGroup();

        client.ClientGroupId.Should().BeNull();
    }

    /// <summary>Builds an active client that belongs to no group.</summary>
    /// <returns>A new, unpersisted client.</returns>
    private static Client NewClient()
    {
        return Client.Create("user-1", "John", "Doe", "john@example.com", Now);
    }
}
