using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>Unit tests for the VAT number check a <see cref="Client"/> records.</summary>
public class ClientVatCheckTests
{
    /// <summary>The moment the first check is recorded.</summary>
    private static readonly DateTimeOffset First = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The moment a later check is recorded.</summary>
    private static readonly DateTimeOffset Later = First.AddDays(30);

    /// <summary>Builds a client in Germany that holds a VAT number.</summary>
    /// <returns>The client.</returns>
    private static Client ClientWithNumber()
    {
        var client = Client.Create("user-1", "Ada", "Lovelace", "ada@example.test", First);
        client.UpdateAddress(null, null, null, null, null, "de");
        client.UpdateSettings(true, false, "DE123456789", null, false, false, false, true, true);
        return client;
    }

    /// <summary>A client with a number and no verdict waits for its first check.</summary>
    [Fact]
    public void NewNumber_IsPending()
    {
        var client = ClientWithNumber();

        client.VatCheckPending.Should().BeTrue();
        client.VatStatus.Should().BeNull();
        client.VatCheckedAt.Should().BeNull();
    }

    /// <summary>A client with no number has nothing to check.</summary>
    [Fact]
    public void NoNumber_IsNotPending()
    {
        var client = Client.Create("user-1", "Ada", "Lovelace", "ada@example.test", First);

        client.VatCheckPending.Should().BeFalse();
    }

    /// <summary>A valid answer stores the verdict, the date and the trimmed name and address the service returned.</summary>
    [Fact]
    public void RecordVatCheck_Valid_StoresTheVerdictNameAndAddress()
    {
        var client = ClientWithNumber();

        client.RecordVatCheck(VatCheckOutcome.Valid, First, " Acme GmbH ", "Main St 1, Berlin");

        client.VatStatus.Should().Be(VatCheckOutcome.Valid);
        client.VatCheckedAt.Should().Be(First);
        client.VatName.Should().Be("Acme GmbH");
        client.VatAddress.Should().Be("Main St 1, Berlin");
        client.VatCheckPending.Should().BeFalse();
    }

    /// <summary>A valid answer without a name or address stores none rather than blanks.</summary>
    [Fact]
    public void RecordVatCheck_Valid_WithBlankDetails_StoresNone()
    {
        var client = ClientWithNumber();

        client.RecordVatCheck(VatCheckOutcome.Valid, First, "  ", null);

        client.VatName.Should().BeNull();
        client.VatAddress.Should().BeNull();
    }

    /// <summary>The details are cut to the columns they are stored in.</summary>
    [Fact]
    public void RecordVatCheck_Valid_CutsLongDetailsToTheirColumns()
    {
        var client = ClientWithNumber();

        client.RecordVatCheck(VatCheckOutcome.Valid, First, new string('n', 300), new string('a', 900));

        client.VatName.Should().HaveLength(Client.VatNameMaxLength);
        client.VatAddress.Should().HaveLength(Client.VatAddressMaxLength);
    }

    /// <summary>An invalid answer stores the verdict and the date and forgets what an earlier valid answer returned.</summary>
    [Fact]
    public void RecordVatCheck_Invalid_StoresTheVerdictAndClearsTheDetails()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.RecordVatCheck(VatCheckOutcome.Invalid, Later, "Ignored", "Ignored");

        client.VatStatus.Should().Be(VatCheckOutcome.Invalid);
        client.VatCheckedAt.Should().Be(Later);
        client.VatName.Should().BeNull();
        client.VatAddress.Should().BeNull();
    }

    /// <summary>An unavailable answer is not a verdict: the earlier status, date, name and address stay.</summary>
    [Fact]
    public void RecordVatCheck_Unavailable_KeepsThePreviousStatus()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.RecordVatCheck(VatCheckOutcome.Unavailable, Later, "Other", "Other");

        client.VatStatus.Should().Be(VatCheckOutcome.Valid);
        client.VatCheckedAt.Should().Be(First);
        client.VatName.Should().Be("Acme GmbH");
        client.VatAddress.Should().Be("Main St 1");
    }

    /// <summary>An unavailable answer never turns a client that was never checked into an invalid one.</summary>
    [Fact]
    public void RecordVatCheck_Unavailable_OnANeverCheckedClient_StoresNothing()
    {
        var client = ClientWithNumber();

        client.RecordVatCheck(VatCheckOutcome.Unavailable, First);

        client.VatStatus.Should().BeNull();
        client.VatCheckedAt.Should().BeNull();
        client.VatCheckPending.Should().BeTrue();
    }

    /// <summary>A client with no number has no check to record.</summary>
    [Fact]
    public void RecordVatCheck_WithNoNumber_Throws()
    {
        var client = Client.Create("user-1", "Ada", "Lovelace", "ada@example.test", First);

        var act = () => client.RecordVatCheck(VatCheckOutcome.Valid, First);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>An outcome that is not defined is refused.</summary>
    [Fact]
    public void RecordVatCheck_WithAnUndefinedOutcome_Throws()
    {
        var client = ClientWithNumber();

        var act = () => client.RecordVatCheck((VatCheckOutcome)99, First);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Changing the number forgets the verdict reached for the old one.</summary>
    [Fact]
    public void ChangingTheNumber_ClearsTheVerdict()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.UpdateSettings(true, false, "DE987654321", null, false, false, false, true, true);

        client.VatStatus.Should().BeNull();
        client.VatCheckedAt.Should().BeNull();
        client.VatName.Should().BeNull();
        client.VatAddress.Should().BeNull();
        client.VatCheckPending.Should().BeTrue();
    }

    /// <summary>Saving the same number again, even with other spacing, keeps the verdict.</summary>
    [Fact]
    public void SavingTheSameNumber_KeepsTheVerdict()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.UpdateSettings(false, false, "  DE123456789 ", null, true, false, false, true, true);

        client.VatStatus.Should().Be(VatCheckOutcome.Valid);
        client.VatCheckedAt.Should().Be(First);
    }

    /// <summary>Removing the number forgets the verdict and leaves nothing pending.</summary>
    [Fact]
    public void RemovingTheNumber_ClearsTheVerdictAndIsNotPending()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.UpdateSettings(true, false, null, null, false, false, false, true, true);

        client.VatStatus.Should().BeNull();
        client.VatCheckPending.Should().BeFalse();
    }

    /// <summary>Moving the billing country forgets the verdict, because a number is registered in one country.</summary>
    [Fact]
    public void ChangingTheCountry_ClearsTheVerdict()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.UpdateAddress(null, null, null, null, null, "FR");

        client.VatStatus.Should().BeNull();
        client.VatCheckedAt.Should().BeNull();
        client.VatCheckPending.Should().BeTrue();
    }

    /// <summary>Saving the address with the same country keeps the verdict.</summary>
    [Fact]
    public void SavingTheSameCountry_KeepsTheVerdict()
    {
        var client = ClientWithNumber();
        client.RecordVatCheck(VatCheckOutcome.Valid, First, "Acme GmbH", "Main St 1");

        client.UpdateAddress("Street 2", null, "Berlin", null, "10115", "DE");

        client.VatStatus.Should().Be(VatCheckOutcome.Valid);
    }
}
