using Innovayse.Hostpanel.Domain.Clients;

namespace Innovayse.Hostpanel.Domain.Tests.Clients;

/// <summary>
/// Unit tests for <see cref="ClientGroup"/>: its fields and their limits, and the rule that setting a
/// group as the default clears the flag on the others.
/// </summary>
public class ClientGroupTests
{
    /// <summary>A valid colour used where the colour is not under test.</summary>
    private const string ValidColor = "#3B82F6";

    /// <summary>Creating a group stores the fields, writes the colour upper case and leaves it not the default.</summary>
    [Fact]
    public void Create_StoresTheFieldsAndIsNotDefault()
    {
        var group = ClientGroup.Create("Wholesale", "Resellers", 12.5m, "#3b82f6");

        group.Name.Should().Be("Wholesale");
        group.Description.Should().Be("Resellers");
        group.DiscountPercent.Should().Be(12.5m);
        group.Color.Should().Be("#3B82F6");
        group.IsDefault.Should().BeFalse();
    }

    /// <summary>The ends of the discount range, 0 and 100 percent, are accepted.</summary>
    /// <param name="discountPercent">A discount on the edge of the range.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_AcceptsTheEdgesOfTheDiscountRange(int discountPercent)
    {
        var group = ClientGroup.Create("Edge", null, discountPercent, ValidColor);

        group.DiscountPercent.Should().Be(discountPercent);
    }

    /// <summary>A discount outside 0 to 100 percent is refused with the discount code.</summary>
    /// <param name="discountPercent">A discount outside the range.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_RefusesADiscountOutsideTheRange(double discountPercent)
    {
        var act = () => ClientGroup.Create("Bad", null, (decimal)discountPercent, ValidColor);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ClientsErrors.ClientGroupDiscountInvalid);
    }

    /// <summary>A colour that is not a hash and six hexadecimal digits is refused with the colour code.</summary>
    /// <param name="color">A malformed colour.</param>
    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("3B82F6")]
    [InlineData("#3B82F")]
    [InlineData("#3B82F66")]
    [InlineData("#GGGGGG")]
    public void Create_RefusesAMalformedColor(string color)
    {
        var act = () => ClientGroup.Create("Bad", null, 10m, color);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ClientsErrors.ClientGroupColorInvalid);
    }

    /// <summary>A blank name, or a name or description longer than its column, is refused.</summary>
    [Fact]
    public void Create_RefusesABlankOrOverlongText()
    {
        Assert.Throws<ArgumentException>(() => ClientGroup.Create(" ", null, 10m, ValidColor));
        Assert.Throws<ArgumentException>(() => ClientGroup.Create(new string('n', ClientGroup.NameMaxLength + 1), null, 10m, ValidColor));
        Assert.Throws<ArgumentException>(() => ClientGroup.Create("Name", new string('d', ClientGroup.DescriptionMaxLength + 1), 10m, ValidColor));
    }

    /// <summary>Updating replaces every field and leaves the default flag as it was.</summary>
    [Fact]
    public void Update_ReplacesTheFieldsAndKeepsTheDefaultFlag()
    {
        var group = ClientGroup.Create("Old", "Old text", 5m, ValidColor);
        group.MakeDefault([]);

        group.Update("New", null, 20m, "#10b981");

        group.Name.Should().Be("New");
        group.Description.Should().BeNull();
        group.DiscountPercent.Should().Be(20m);
        group.Color.Should().Be("#10B981");
        group.IsDefault.Should().BeTrue();
    }

    /// <summary>A refused update leaves the group as it was.</summary>
    [Fact]
    public void Update_Refused_ChangesNothing()
    {
        var group = ClientGroup.Create("Keep", "Keep text", 5m, ValidColor);

        var act = () => group.Update("Changed", "Changed text", 150m, ValidColor);

        act.Should().Throw<DomainRuleException>();
        group.Name.Should().Be("Keep");
        group.Description.Should().Be("Keep text");
        group.DiscountPercent.Should().Be(5m);
    }

    /// <summary>Making a group the default clears the flag on the other group that held it, and leaves a group outside the list alone.</summary>
    [Fact]
    public void MakeDefault_ClearsTheFlagOnTheOthers()
    {
        var previous = ClientGroup.Create("Previous", null, 0m, ValidColor);
        previous.MakeDefault([]);
        var next = ClientGroup.Create("Next", null, 0m, ValidColor);
        var outside = ClientGroup.Create("Outside", null, 0m, ValidColor);
        outside.MakeDefault([]);

        next.MakeDefault([previous, next]);

        next.IsDefault.Should().BeTrue();
        previous.IsDefault.Should().BeFalse();
        outside.IsDefault.Should().BeTrue();
    }

    /// <summary>Clearing the flag makes the group not the default, and clearing it again changes nothing.</summary>
    [Fact]
    public void ClearDefault_StopsTheGroupBeingTheDefault()
    {
        var group = ClientGroup.Create("Default", null, 0m, ValidColor);
        group.MakeDefault([]);

        group.ClearDefault();
        group.ClearDefault();

        group.IsDefault.Should().BeFalse();
    }

    /// <summary>The colour check accepts both letter cases and refuses null.</summary>
    [Fact]
    public void IsValidColor_AcceptsEitherCaseAndRefusesNull()
    {
        ClientGroup.IsValidColor("#abcDEF").Should().BeTrue();
        ClientGroup.IsValidColor(null).Should().BeFalse();
    }
}
