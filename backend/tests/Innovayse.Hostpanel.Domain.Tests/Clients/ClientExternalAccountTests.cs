using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>
/// Unit tests for the external account link on <see cref="Client"/>: the provider and the account id
/// are set together, cleared together and never stored one without the other.
/// </summary>
public class ClientExternalAccountTests
{
    /// <summary>The fixed moment every domain method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A new client has no external account link.</summary>
    [Fact]
    public void Create_HasNoExternalAccount()
    {
        var client = NewClient();

        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>Setting the link stores both values.</summary>
    [Fact]
    public void SetExternalAccount_StoresBothValues()
    {
        var client = NewClient();

        client.SetExternalAccount("crm", "ACC-42");

        Assert.Equal("crm", client.ExternalAccountProvider);
        Assert.Equal("ACC-42", client.ExternalAccountId);
    }

    /// <summary>Surrounding space is trimmed from both values before they are stored.</summary>
    [Fact]
    public void SetExternalAccount_TrimsBothValues()
    {
        var client = NewClient();

        client.SetExternalAccount("  crm ", " ACC-42  ");

        Assert.Equal("crm", client.ExternalAccountProvider);
        Assert.Equal("ACC-42", client.ExternalAccountId);
    }

    /// <summary>Setting a new link replaces the earlier one whole.</summary>
    [Fact]
    public void SetExternalAccount_ReplacesAnEarlierLink()
    {
        var client = NewClient();
        client.SetExternalAccount("crm", "ACC-42");

        client.SetExternalAccount("erp", "C-7");

        Assert.Equal("erp", client.ExternalAccountProvider);
        Assert.Equal("C-7", client.ExternalAccountId);
    }

    /// <summary>Clearing the link removes both values.</summary>
    [Fact]
    public void ClearExternalAccount_RemovesBothValues()
    {
        var client = NewClient();
        client.SetExternalAccount("crm", "ACC-42");

        client.ClearExternalAccount();

        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>Clearing a client that has no link is allowed and leaves it without one.</summary>
    [Fact]
    public void ClearExternalAccount_WithoutALink_DoesNothing()
    {
        var client = NewClient();

        client.ClearExternalAccount();

        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>A link without a provider is refused, so an account id is never stored alone.</summary>
    /// <param name="provider">A missing or blank provider.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetExternalAccount_WithoutAProvider_IsRefused(string? provider)
    {
        var client = NewClient();

        var ex = Assert.Throws<ArgumentException>(() => client.SetExternalAccount(provider!, "ACC-42"));

        Assert.Equal("provider", ex.ParamName);
        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>A link without an account id is refused, so a provider is never stored alone.</summary>
    /// <param name="accountId">A missing or blank account id.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetExternalAccount_WithoutAnAccountId_IsRefused(string? accountId)
    {
        var client = NewClient();

        var ex = Assert.Throws<ArgumentException>(() => client.SetExternalAccount("crm", accountId!));

        Assert.Equal("accountId", ex.ParamName);
        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>A refused change leaves an existing link whole, never half replaced.</summary>
    [Fact]
    public void SetExternalAccount_WhenRefused_KeepsTheExistingLink()
    {
        var client = NewClient();
        client.SetExternalAccount("crm", "ACC-42");

        Assert.Throws<ArgumentException>(() => client.SetExternalAccount("erp", " "));

        Assert.Equal("crm", client.ExternalAccountProvider);
        Assert.Equal("ACC-42", client.ExternalAccountId);
    }

    /// <summary>The longest provider and account id the columns hold are accepted.</summary>
    [Fact]
    public void SetExternalAccount_AtTheMaximumLengths_IsAccepted()
    {
        var client = NewClient();
        var provider = new string('p', 64);
        var accountId = new string('a', 256);

        client.SetExternalAccount(provider, accountId);

        Assert.Equal(provider, client.ExternalAccountProvider);
        Assert.Equal(accountId, client.ExternalAccountId);
    }

    /// <summary>The length limits are checked on the trimmed value, so surrounding space does not count.</summary>
    [Fact]
    public void SetExternalAccount_LengthIsCheckedAfterTrimming()
    {
        var client = NewClient();

        client.SetExternalAccount($" {new string('p', 64)} ", $" {new string('a', 256)} ");

        Assert.Equal(64, client.ExternalAccountProvider!.Length);
        Assert.Equal(256, client.ExternalAccountId!.Length);
    }

    /// <summary>A provider longer than its column is refused.</summary>
    [Fact]
    public void SetExternalAccount_WithAProviderTooLong_IsRefused()
    {
        var client = NewClient();

        var ex = Assert.Throws<ArgumentException>(() => client.SetExternalAccount(new string('p', 65), "ACC-42"));

        Assert.Equal("provider", ex.ParamName);
        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>An account id longer than its column is refused.</summary>
    [Fact]
    public void SetExternalAccount_WithAnAccountIdTooLong_IsRefused()
    {
        var client = NewClient();

        var ex = Assert.Throws<ArgumentException>(() => client.SetExternalAccount("crm", new string('a', 257)));

        Assert.Equal("accountId", ex.ParamName);
        Assert.Null(client.ExternalAccountProvider);
        Assert.Null(client.ExternalAccountId);
    }

    /// <summary>Builds an active client with no external account link.</summary>
    /// <returns>A new, unpersisted client.</returns>
    private static Client NewClient()
    {
        return Client.Create("user-1", "John", "Doe", "john@example.com", Now);
    }
}
