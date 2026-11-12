using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>The fraud detection setting vocabulary.</summary>
public sealed class FraudSettingKeysTests
{
    /// <summary>Every key has a seeded default and a pattern, and every default passes its own pattern.</summary>
    [Fact]
    public void Defaults_MatchTheirPatterns_AndCoverEveryKey()
    {
        FraudSettingKeys.Defaults.Select(d => d.Key).Should().BeEquivalentTo(FraudSettingKeys.All);
        FraudSettingKeys.Patterns.Keys.Should().BeEquivalentTo(FraudSettingKeys.All);

        foreach (var (key, value, _) in FraudSettingKeys.Defaults)
        {
            FraudSettingKeys.Patterns[key].Regex.IsMatch(value).Should().BeTrue(key);
        }
    }

    /// <summary>A fresh install checks nothing until the operator turns it on, and starts at the thresholds 50 and 90.</summary>
    [Fact]
    public void Defaults_AreOffWithReviewAtFiftyAndRejectAtNinety()
    {
        string Default(string key) => FraudSettingKeys.Defaults.Single(d => d.Key == key).Value;

        Default(FraudSettingKeys.Enabled).Should().Be("false");
        Default(FraudSettingKeys.ManualReviewRiskScore).Should().Be("50");
        Default(FraudSettingKeys.AutoRejectRiskScore).Should().Be("90");
    }

    /// <summary>A risk score is a whole number from 0 to 100 without leading zeros.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether a score accepts it.</param>
    [Theory]
    [InlineData("0", true)]
    [InlineData("7", true)]
    [InlineData("50", true)]
    [InlineData("99", true)]
    [InlineData("100", true)]
    [InlineData("101", false)]
    [InlineData("-1", false)]
    [InlineData("05", false)]
    [InlineData("1.5", false)]
    [InlineData("", false)]
    public void RiskScore_Pattern(string value, bool ok)
    {
        FraudSettingKeys.Patterns[FraudSettingKeys.ManualReviewRiskScore].Regex.IsMatch(value).Should().Be(ok);
        FraudSettingKeys.Patterns[FraudSettingKeys.AutoRejectRiskScore].Regex.IsMatch(value).Should().Be(ok);
    }

    /// <summary>The velocity window is a whole number of minutes from 1 to 1440.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether the window accepts it.</param>
    [Theory]
    [InlineData("1", true)]
    [InlineData("60", true)]
    [InlineData("999", true)]
    [InlineData("1000", true)]
    [InlineData("1439", true)]
    [InlineData("1440", true)]
    [InlineData("0", false)]
    [InlineData("1441", false)]
    [InlineData("01", false)]
    [InlineData("abc", false)]
    public void VelocityWindow_Pattern(string value, bool ok)
    {
        FraudSettingKeys.Patterns[FraudSettingKeys.VelocityWindowMinutes].Regex.IsMatch(value).Should().Be(ok);
    }

    /// <summary>The order and account limits are whole numbers from 1 to 100.</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="ok">Whether a limit accepts it.</param>
    [Theory]
    [InlineData("1", true)]
    [InlineData("99", true)]
    [InlineData("100", true)]
    [InlineData("0", false)]
    [InlineData("101", false)]
    [InlineData("007", false)]
    public void Limits_Pattern(string value, bool ok)
    {
        FraudSettingKeys.Patterns[FraudSettingKeys.VelocityMaxOrders].Regex.IsMatch(value).Should().Be(ok);
        FraudSettingKeys.Patterns[FraudSettingKeys.MaxAccountsPerIp].Regex.IsMatch(value).Should().Be(ok);
    }

    /// <summary>The switch takes true or false in any case and is stored in lower case.</summary>
    [Fact]
    public void Enabled_AcceptsTrueAndFalseInAnyCase_StoredLowerCase()
    {
        var pattern = FraudSettingKeys.Patterns[FraudSettingKeys.Enabled];

        pattern.Regex.IsMatch("TRUE").Should().BeTrue();
        pattern.Regex.IsMatch("yes").Should().BeFalse();
        pattern.Canonical("False").Should().Be("false");
    }

    /// <summary>The fraud settings are in the general catalogue, which is how they are seeded and written, and none is public.</summary>
    [Fact]
    public void Keys_AreInTheCatalogue_AndNotPublic()
    {
        foreach (var key in FraudSettingKeys.All)
        {
            GeneralSettingKeys.Contains(key).Should().BeTrue(key);
        }

        FraudSettingKeys.All.Should().NotIntersectWith(PortalSettingKeys.Public);
    }
}
