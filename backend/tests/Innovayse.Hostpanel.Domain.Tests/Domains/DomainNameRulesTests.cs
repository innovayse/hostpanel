using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for <see cref="DomainNameRules"/>, the one rule for what a domain name looks like.</summary>
public sealed class DomainNameRulesTests
{
    /// <summary>Ordinary names, a nested one, a hyphen inside a label, digits and an encoded internationalised label pass.</summary>
    /// <param name="name">A well-formed name.</param>
    [Theory]
    [InlineData("example.com")]
    [InlineData("shop.example.co.uk")]
    [InlineData("my-shop.com")]
    [InlineData("123.io")]
    [InlineData("xn--bcher-kva.de")]
    [InlineData("EXAMPLE.COM")]
    public void IsValidName_AcceptsAWellFormedName(string name)
    {
        DomainNameRules.IsValidName(name).Should().BeTrue();
    }

    /// <summary>Text that is no name at all is refused, whatever a registrar would answer for it.</summary>
    /// <param name="name">A malformed name.</param>
    [Theory]
    [InlineData("bad name!.com")]
    [InlineData("bad name.com")]
    [InlineData("-bad-.com")]
    [InlineData("-bad.com")]
    [InlineData("bad-.com")]
    [InlineData("bad_name.com")]
    [InlineData("bad!.com")]
    [InlineData("example")]
    [InlineData("example.")]
    [InlineData(".com")]
    [InlineData("exa..mple.com")]
    [InlineData("example.-com")]
    [InlineData("example.com/path")]
    [InlineData("münchen.de")]
    [InlineData("example.münchen")]
    [InlineData("example.123")]
    [InlineData("example.c")]
    [InlineData("example.c0m")]
    [InlineData("example.xn--p1ai")]
    [InlineData("  example.com  ")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidName_RefusesAMalformedName(string name)
    {
        DomainNameRules.IsValidName(name).Should().BeFalse();
    }

    /// <summary>
    /// A name registration would refuse is no name here: the rule is the one registration holds, so a name
    /// that passes it can be ordered as typed.
    /// </summary>
    [Fact]
    public void IsValidName_HoldsTheRuleRegistrationHolds()
    {
        DomainNameRules.IsValidName("shop.example.com").Should().BeTrue();
        DomainNameRules.IsValidName("a.co").Should().BeTrue();
        DomainNameRules.IsValidName("example.com ").Should().BeFalse();
        DomainNameRules.IsValidName("example.com\n").Should().BeFalse();
    }

    /// <summary>A missing text is not a name.</summary>
    [Fact]
    public void IsValidName_RefusesNull()
    {
        DomainNameRules.IsValidName(null).Should().BeFalse();
    }

    /// <summary>A label of the longest allowed length passes and one character more does not.</summary>
    [Fact]
    public void IsValidName_HoldsALabelToSixtyThreeCharacters()
    {
        var longest = new string('a', DomainNameRules.MaxLabelLength);
        var tooLong = new string('a', DomainNameRules.MaxLabelLength + 1);

        DomainNameRules.IsValidName($"{longest}.com").Should().BeTrue();
        DomainNameRules.IsValidName($"{tooLong}.com").Should().BeFalse();
    }

    /// <summary>A whole name over 253 characters is refused even when each label is short enough.</summary>
    [Fact]
    public void IsValidName_HoldsTheWholeNameToTheLimit()
    {
        var label = new string('a', 50);
        var within = string.Join('.', label, label, label, label, "com");
        var over = string.Join('.', label, label, label, label, label, "com");

        within.Length.Should().BeLessOrEqualTo(DomainNameRules.MaxNameLength);
        over.Length.Should().BeGreaterThan(DomainNameRules.MaxNameLength);
        DomainNameRules.IsValidName(within).Should().BeTrue();
        DomainNameRules.IsValidName(over).Should().BeFalse();
    }

    /// <summary>A single label is judged on its own: hyphens only inside, 1 to 63 characters.</summary>
    [Fact]
    public void IsValidLabel_JudgesOneLabel()
    {
        DomainNameRules.IsValidLabel("a-b").Should().BeTrue();
        DomainNameRules.IsValidLabel("a").Should().BeTrue();
        DomainNameRules.IsValidLabel(string.Empty).Should().BeFalse();
        DomainNameRules.IsValidLabel("-a").Should().BeFalse();
        DomainNameRules.IsValidLabel("a-").Should().BeFalse();
        DomainNameRules.IsValidLabel("a b").Should().BeFalse();
        DomainNameRules.IsValidLabel("münchen").Should().BeFalse();
    }

    /// <summary>An extension is at least two letters and never a number or a mix of letters and digits.</summary>
    [Fact]
    public void IsValidExtension_NeedsTwoOrMoreLetters()
    {
        DomainNameRules.IsValidExtension("com").Should().BeTrue();
        DomainNameRules.IsValidExtension("io").Should().BeTrue();
        DomainNameRules.IsValidExtension("c").Should().BeFalse();
        DomainNameRules.IsValidExtension("123").Should().BeFalse();
        DomainNameRules.IsValidExtension("c0m").Should().BeFalse();
        DomainNameRules.IsValidExtension("xn--p1ai").Should().BeFalse();
        DomainNameRules.IsValidExtension(string.Empty).Should().BeFalse();
    }
}
