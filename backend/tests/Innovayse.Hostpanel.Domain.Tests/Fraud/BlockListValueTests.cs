using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for the validation and normalisation of block list values in <see cref="BlockListValue"/>.</summary>
public sealed class BlockListValueTests
{
    /// <summary>Valid values are accepted and stored in their canonical spelling.</summary>
    /// <param name="type">What the value names.</param>
    /// <param name="value">The value as typed.</param>
    /// <param name="expected">The stored spelling.</param>
    [Theory]
    [InlineData(BlockListType.Ip, " 203.0.113.9 ", "203.0.113.9")]
    [InlineData(BlockListType.Ip, "::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData(BlockListType.Ip, "2001:DB8::1", "2001:db8::1")]
    [InlineData(BlockListType.Cidr, "203.0.113.77/24", "203.0.113.0/24")]
    [InlineData(BlockListType.EmailDomain, "@Mailinator.COM", "mailinator.com")]
    [InlineData(BlockListType.EmailDomain, "mail.example.co", "mail.example.co")]
    [InlineData(BlockListType.Country, "am", "AM")]
    public void TryNormalise_Valid_ReturnsCanonicalSpelling(BlockListType type, string value, string expected)
    {
        BlockListValue.TryNormalise(type, value, out var normalised).Should().BeTrue();

        normalised.Should().Be(expected);
    }

    /// <summary>A value that does not fit its type is refused.</summary>
    /// <param name="type">What the value names.</param>
    /// <param name="value">The value as typed.</param>
    [Theory]
    [InlineData(BlockListType.Ip, "203.0.113.0/24")]
    [InlineData(BlockListType.Ip, "nope")]
    [InlineData(BlockListType.Cidr, "203.0.113.9")]
    [InlineData(BlockListType.EmailDomain, "localhost")]
    [InlineData(BlockListType.EmailDomain, "-bad.example.com")]
    [InlineData(BlockListType.EmailDomain, "exa mple.com")]
    [InlineData(BlockListType.Country, "ARM")]
    [InlineData(BlockListType.Country, "A1")]
    [InlineData(BlockListType.Country, "")]
    [InlineData(BlockListType.Ip, null)]
    public void TryNormalise_Invalid_IsRefused(BlockListType type, string? value)
    {
        BlockListValue.TryNormalise(type, value, out _).Should().BeFalse();
    }

    /// <summary>A type the product does not know has no valid value.</summary>
    [Fact]
    public void TryNormalise_UnknownType_IsRefused()
    {
        BlockListValue.TryNormalise((BlockListType)99, "x.example.com", out _).Should().BeFalse();
    }

    /// <summary>A value longer than the column is refused.</summary>
    [Fact]
    public void TryNormalise_TooLong_IsRefused()
    {
        var domain = string.Join('.', Enumerable.Repeat(new string('a', 50), 6)) + ".com";

        BlockListValue.TryNormalise(BlockListType.EmailDomain, domain, out _).Should().BeFalse();
    }
}
