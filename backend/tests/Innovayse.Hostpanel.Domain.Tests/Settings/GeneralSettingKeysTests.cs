using Innovayse.Hostpanel.Domain.Settings;

namespace Innovayse.Hostpanel.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="GeneralSettingKeys"/>: the catalogue is exactly the portal, billing,
/// plugin-system and campaign defaults, and holds no secret.
/// </summary>
public class GeneralSettingKeysTests
{
    /// <summary>The catalogue is the union of the four default lists, nothing more and nothing less.</summary>
    [Fact]
    public void All_IsTheUnionOfThePortalBillingPluginAndCampaignDefaults()
    {
        var union = PortalSettingKeys.Defaults
            .Concat(BillingSettingKeys.Defaults)
            .Concat(PluginSettingKeys.Defaults)
            .Concat(CampaignSettingKeys.Defaults)
            .Select(d => d.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(union.SetEquals(GeneralSettingKeys.All));
        Assert.Equal(union.Count, GeneralSettingKeys.Defaults.Count);
    }

    /// <summary>No catalogue key is a secret, so a general write never carries a credential.</summary>
    [Fact]
    public void All_HoldsNoSecretKey()
    {
        Assert.DoesNotContain(GeneralSettingKeys.All, SecretSettingKeys.IsSecret);
    }

    /// <summary>Keys outside the catalogue are not members: integration rows, the setup token, deployment keys.</summary>
    /// <param name="key">The full setting key.</param>
    [Theory]
    [InlineData("integration:gateway:secret_key")]
    [InlineData("integration:gateway:is_enabled")]
    [InlineData(SecretSettingKeys.SetupToken)]
    [InlineData("company.name")]
    [InlineData("PORTAL.TEMPLATE")]
    public void Contains_KeyOutsideTheCatalogue_IsFalse(string key)
    {
        Assert.False(GeneralSettingKeys.Contains(key));
    }

    /// <summary>One key from each source list is a member.</summary>
    /// <param name="key">The full setting key.</param>
    [Theory]
    [InlineData(PortalSettingKeys.Template)]
    [InlineData(BillingSettingKeys.InvoiceDaysBefore)]
    [InlineData(PluginSettingKeys.AllowUnsigned)]
    [InlineData(CampaignSettingKeys.SendRatePerMinute)]
    public void Contains_CatalogueKey_IsTrue(string key)
    {
        Assert.True(GeneralSettingKeys.Contains(key));
    }

    /// <summary>A pattern is found whichever group the key belongs to, and a key without one has none.</summary>
    /// <param name="key">The full setting key.</param>
    /// <param name="found">Whether the key has a pattern.</param>
    [Theory]
    [InlineData(BillingSettingKeys.InvoiceDaysBefore, true)]
    [InlineData(CampaignSettingKeys.SendRatePerMinute, true)]
    [InlineData(PortalSettingKeys.Template, false)]
    [InlineData("company.name", false)]
    public void TryGetPattern_FindsThePatternOfAnyGroup(string key, bool found)
    {
        Assert.Equal(found, GeneralSettingKeys.TryGetPattern(key, out _));
    }

    /// <summary>Only the keys whose empty value means "unset" may be saved empty.</summary>
    /// <param name="key">The full setting key.</param>
    /// <param name="allowed">Whether an empty value is accepted.</param>
    [Theory]
    [InlineData(BillingSettingKeys.ReminderDays, true)]
    [InlineData(BillingSettingKeys.InvoiceDaysBefore, false)]
    [InlineData(CampaignSettingKeys.SendRatePerMinute, false)]
    public void AllowsEmpty_CoversEveryGroup(string key, bool allowed)
    {
        Assert.Equal(allowed, GeneralSettingKeys.AllowsEmpty(key));
    }
}
