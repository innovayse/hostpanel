using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for <see cref="DomainExtendedAttributes"/>, the registry's extra values kept with an order line and a domain.</summary>
public sealed class DomainExtendedAttributesTests
{
    /// <summary>No values is the empty set: it holds nothing and stores nothing.</summary>
    [Fact]
    public void Empty_HoldsNothingAndStoresNothing()
    {
        DomainExtendedAttributes.Empty.IsEmpty.Should().BeTrue();
        DomainExtendedAttributes.Empty.Serialize().Should().BeNull();
        DomainExtendedAttributes.From(null).IsEmpty.Should().BeTrue();
    }

    /// <summary>A blank value is no value: an optional field left empty is neither stored nor sent.</summary>
    [Fact]
    public void From_LeavesOutBlankValues()
    {
        var attributes = DomainExtendedAttributes.From(
            new Dictionary<string, string> { ["first"] = "one", ["second"] = string.Empty, ["third"] = "   " });

        attributes.Values.Should().BeEquivalentTo(new Dictionary<string, string> { ["first"] = "one" });
    }

    /// <summary>The stored form reads back as the same values, text with quotes and non-ASCII letters included.</summary>
    [Fact]
    public void Serialize_ThenParse_RoundTripsTheValues()
    {
        var attributes = DomainExtendedAttributes.From(
            new Dictionary<string, string> { ["note"] = "a \"quoted\" Ünïcode value", ["key:two"] = "2" });

        var parsed = DomainExtendedAttributes.Parse(attributes.Serialize());

        parsed.Should().Be(attributes);
        parsed.Values.Should().BeEquivalentTo(attributes.Values);
    }

    /// <summary>Nothing stored reads as the empty set, whether the column holds null or an empty text.</summary>
    /// <param name="stored">The stored text.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NothingStored_IsEmpty(string? stored)
    {
        DomainExtendedAttributes.Parse(stored).IsEmpty.Should().BeTrue();
    }

    /// <summary>The same values give the same stored text whatever the order they were given in, so equal sets are equal.</summary>
    [Fact]
    public void Serialize_IsTheSameWhateverTheOrderOfTheInput()
    {
        var one = DomainExtendedAttributes.From(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var other = DomainExtendedAttributes.From(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        one.Serialize().Should().Be(other.Serialize());
        one.Should().Be(other);
        one.GetHashCode().Should().Be(other.GetHashCode());
    }

    /// <summary>A different value is a different set.</summary>
    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var one = DomainExtendedAttributes.From(new Dictionary<string, string> { ["a"] = "1" });
        var other = DomainExtendedAttributes.From(new Dictionary<string, string> { ["a"] = "2" });

        (one == other).Should().BeFalse();
    }

    /// <summary>Values whose stored form is longer than the column holds are refused, so a save can never fail on width.</summary>
    [Fact]
    public void TryCreate_ValuesTooLongToStore_AreRefused()
    {
        var tooLong = new Dictionary<string, string> { ["note"] = new string('x', DomainExtendedAttributes.MaxSerializedLength) };

        DomainExtendedAttributes.TryCreate(tooLong, out var attributes).Should().BeFalse();
        attributes.Should().BeNull();
    }

    /// <summary>Values that just fit are accepted.</summary>
    [Fact]
    public void TryCreate_ValuesThatFit_AreAccepted()
    {
        var fits = new Dictionary<string, string> { ["note"] = new string('x', DomainExtendedAttributes.MaxSerializedLength - 20) };

        DomainExtendedAttributes.TryCreate(fits, out var attributes).Should().BeTrue();
        attributes!.Serialize()!.Length.Should().BeLessThanOrEqualTo(DomainExtendedAttributes.MaxSerializedLength);
    }

    /// <summary>Building from values too long to store throws the field refusal code, the last line of defence behind the validation.</summary>
    [Fact]
    public void From_ValuesTooLongToStore_ThrowsTheFieldInvalidCode()
    {
        var tooLong = new Dictionary<string, string> { ["note"] = new string('x', DomainExtendedAttributes.MaxSerializedLength) };

        var act = () => DomainExtendedAttributes.From(tooLong);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(DomainsErrors.DomainFieldInvalid);
    }
}
