namespace Innovayse.Domain.Tests.Clients;

using Innovayse.Domain.Clients;

/// <summary>The currency a <see cref="Client"/> is billed in: recorded once, in one shape.</summary>
public sealed class ClientCurrencyTests
{
    /// <summary>A code is stored upper case whatever the caller typed, so "amd" and "AMD" are one currency.</summary>
    [Fact]
    public void SetCurrency_UpperCasesTheCode()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com");

        client.SetCurrency("amd");

        Assert.Equal("AMD", client.Currency);
    }

    /// <summary>Anything but a three-letter code is refused: an invoice cannot be raised in "US$".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void SetCurrency_RejectsACodeThatIsNotThreeLetters(string code)
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com");

        Assert.Throws<ArgumentException>(() => client.SetCurrency(code));
        Assert.Null(client.Currency);
    }

    /// <summary>The other preferences no longer carry the currency, so writing them leaves it alone.</summary>
    [Fact]
    public void UpdatePreferences_DoesNotTouchTheCurrency()
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com");
        client.SetCurrency("USD");

        client.UpdatePreferences("bank_transfer", "billing@example.com", "note");

        Assert.Equal("USD", client.Currency);
        Assert.Equal("bank_transfer", client.PaymentMethod);
    }
}
