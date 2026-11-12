using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>The campaign setting vocabulary.</summary>
public sealed class CampaignSettingKeysTests
{
    /// <summary>Every default passes its own pattern, so a fresh install never starts with a value the admin would refuse.</summary>
    [Fact]
    public void Defaults_MatchTheirPatterns()
    {
        foreach (var (key, value, _) in CampaignSettingKeys.Defaults)
        {
            CampaignSettingKeys.Patterns[key].Regex.IsMatch(value).Should().BeTrue(key);
        }
    }

    /// <summary>The send rate is seeded at 60 mails a minute.</summary>
    [Fact]
    public void SendRatePerMinute_IsSeededAtSixty()
    {
        CampaignSettingKeys.Defaults.Single(d => d.Key == CampaignSettingKeys.SendRatePerMinute).Value.Should().Be("60");
    }

    /// <summary>The send rate takes a whole number from 1 to 600 and nothing else.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether the send rate accepts it.</param>
    [Theory]
    [InlineData("1", true)]
    [InlineData("9", true)]
    [InlineData("10", true)]
    [InlineData("99", true)]
    [InlineData("100", true)]
    [InlineData("599", true)]
    [InlineData("600", true)]
    [InlineData("0", false)]
    [InlineData("601", false)]
    [InlineData("700", false)]
    [InlineData("1000", false)]
    [InlineData("01", false)]
    [InlineData("-5", false)]
    [InlineData("1.5", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void SendRatePerMinute_Pattern(string value, bool ok)
    {
        CampaignSettingKeys.Patterns[CampaignSettingKeys.SendRatePerMinute].Regex.IsMatch(value).Should().Be(ok);
    }

    /// <summary>The refusal text names the range.</summary>
    [Fact]
    public void SendRatePerMinute_ExpectedText()
    {
        CampaignSettingKeys.Patterns[CampaignSettingKeys.SendRatePerMinute].Expected.Should().Be("a whole number of mails per minute from 1 to 600");
    }

    /// <summary>No campaign key is public: none of them is served by the anonymous storefront endpoint.</summary>
    [Fact]
    public void Keys_AreNotPublic()
    {
        CampaignSettingKeys.Defaults.Select(d => d.Key).Should().NotIntersectWith(PortalSettingKeys.Public);
    }
}
