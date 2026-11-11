using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Campaigns;

/// <summary>Unit tests for how <see cref="EmailCampaignSegment"/> checks its filter value, built through a campaign.</summary>
public sealed class EmailCampaignSegmentTests
{
    /// <summary>Builds a campaign with one segment and returns that segment.</summary>
    /// <param name="filterType">The filter of the segment.</param>
    /// <param name="value">The value of the segment.</param>
    /// <returns>The stored segment.</returns>
    private static EmailCampaignSegment Segment(CampaignFilterType filterType, string? value)
    {
        var campaign = EmailCampaign.Create("Subject", "Body", "admin-1", [new(filterType, value)]);
        return Assert.Single(campaign.Segments);
    }

    /// <summary>Asserts a segment definition is refused with the segment value code.</summary>
    /// <param name="filterType">The filter of the segment.</param>
    /// <param name="value">The value of the segment.</param>
    private static void AssertRefused(CampaignFilterType filterType, string? value)
    {
        var ex = Assert.Throws<DomainRuleException>(() => Segment(filterType, value));

        Assert.Equal(CampaignErrors.SegmentValueInvalid, ex.Code);
    }

    /// <summary>Accepts agrees with the campaign: a pair it builds is accepted, a pair it refuses is not.</summary>
    /// <param name="filterType">The filter of the segment.</param>
    /// <param name="value">The value of the segment.</param>
    /// <param name="expected">Whether the pair fits.</param>
    [Theory]
    [InlineData(CampaignFilterType.AllActive, null, true)]
    [InlineData(CampaignFilterType.AllActive, "5", false)]
    [InlineData(CampaignFilterType.Product, "12", true)]
    [InlineData(CampaignFilterType.Product, "0", false)]
    [InlineData(CampaignFilterType.Product, null, false)]
    [InlineData(CampaignFilterType.Country, "am", true)]
    [InlineData(CampaignFilterType.Country, "ARM", false)]
    [InlineData(CampaignFilterType.ServiceExpiringInDays, "30", true)]
    [InlineData(CampaignFilterType.ServiceExpiringInDays, "366", false)]
    [InlineData(CampaignFilterType.ClientGroup, "4", true)]
    [InlineData(CampaignFilterType.ClientGroup, "0", false)]
    [InlineData(CampaignFilterType.ClientGroup, null, false)]
    [InlineData(CampaignFilterType.NotSignedInForDays, "90", true)]
    [InlineData(CampaignFilterType.NotSignedInForDays, "3651", false)]
    [InlineData((CampaignFilterType)99, null, false)]
    public void Accepts_AgreesWithTheCampaign(CampaignFilterType filterType, string? value, bool expected)
    {
        Assert.Equal(expected, EmailCampaignSegment.Accepts(filterType, value));
    }

    /// <summary>All active takes no value; a blank one is stored as null.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AllActive_WithoutValue_StoresNull(string? value)
    {
        var segment = Segment(CampaignFilterType.AllActive, value);

        Assert.Equal(CampaignFilterType.AllActive, segment.FilterType);
        Assert.Null(segment.FilterValue);
    }

    /// <summary>All active refuses a value.</summary>
    [Fact]
    public void AllActive_WithValue_Throws()
    {
        AssertRefused(CampaignFilterType.AllActive, "5");
    }

    /// <summary>A product filter stores the product id.</summary>
    [Fact]
    public void Product_WithId_StoresIt()
    {
        Assert.Equal("12", Segment(CampaignFilterType.Product, " 12 ").FilterValue);
    }

    /// <summary>A product filter refuses a missing, non-numeric, zero or negative id.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1.5")]
    public void Product_WithInvalidId_Throws(string? value)
    {
        AssertRefused(CampaignFilterType.Product, value);
    }

    /// <summary>A country filter stores the code upper-cased.</summary>
    [Fact]
    public void Country_WithCode_StoresItUpperCase()
    {
        Assert.Equal("AM", Segment(CampaignFilterType.Country, " am ").FilterValue);
    }

    /// <summary>A country filter refuses a missing code, a wrong length and non-letters.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("ARM")]
    [InlineData("1A")]
    public void Country_WithInvalidCode_Throws(string? value)
    {
        AssertRefused(CampaignFilterType.Country, value);
    }

    /// <summary>An expiry filter stores the number of days, including the largest allowed.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData("1")]
    [InlineData("30")]
    [InlineData("365")]
    public void ServiceExpiringInDays_WithDays_StoresThem(string value)
    {
        Assert.Equal(value, Segment(CampaignFilterType.ServiceExpiringInDays, value).FilterValue);
    }

    /// <summary>An expiry filter refuses a missing, zero, negative, oversized or non-numeric count.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("366")]
    [InlineData("soon")]
    public void ServiceExpiringInDays_WithInvalidDays_Throws(string? value)
    {
        AssertRefused(CampaignFilterType.ServiceExpiringInDays, value);
    }

    /// <summary>A client group filter stores the group id.</summary>
    [Fact]
    public void ClientGroup_WithId_StoresIt()
    {
        Assert.Equal("4", Segment(CampaignFilterType.ClientGroup, " 4 ").FilterValue);
    }

    /// <summary>A client group filter refuses a missing, non-numeric, zero or negative id.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vip")]
    [InlineData("0")]
    [InlineData("-2")]
    public void ClientGroup_WithInvalidId_Throws(string? value)
    {
        AssertRefused(CampaignFilterType.ClientGroup, value);
    }

    /// <summary>A sign-in filter stores the number of days, including the largest allowed.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData("1")]
    [InlineData("90")]
    [InlineData("3650")]
    public void NotSignedInForDays_WithDays_StoresThem(string value)
    {
        Assert.Equal(value, Segment(CampaignFilterType.NotSignedInForDays, value).FilterValue);
    }

    /// <summary>A sign-in filter refuses a missing, zero, negative, oversized or non-numeric count.</summary>
    /// <param name="value">The segment value as given.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("3651")]
    [InlineData("long ago")]
    public void NotSignedInForDays_WithInvalidDays_Throws(string? value)
    {
        AssertRefused(CampaignFilterType.NotSignedInForDays, value);
    }

    /// <summary>A filter type the enum does not name is refused.</summary>
    [Fact]
    public void UnknownFilterType_Throws()
    {
        AssertRefused((CampaignFilterType)99, null);
    }

    /// <summary>Normalize puts a value in the shape it is stored in, so a count over unsaved segments reads the same as a saved campaign.</summary>
    /// <param name="filterType">The filter of the segment.</param>
    /// <param name="value">The value as typed.</param>
    /// <param name="expected">The value as stored.</param>
    [Theory]
    [InlineData(CampaignFilterType.AllActive, null, null)]
    [InlineData(CampaignFilterType.Product, " 12 ", "12")]
    [InlineData(CampaignFilterType.Country, " am ", "AM")]
    [InlineData(CampaignFilterType.ServiceExpiringInDays, "30", "30")]
    [InlineData(CampaignFilterType.ClientGroup, " 4 ", "4")]
    [InlineData(CampaignFilterType.NotSignedInForDays, "90", "90")]
    public void Normalize_PutsTheValueInItsStoredShape(CampaignFilterType filterType, string? value, string? expected)
    {
        Assert.Equal(expected, EmailCampaignSegment.Normalize(filterType, value));
        Assert.Equal(expected, Segment(filterType, value).FilterValue);
    }

    /// <summary>Normalize refuses a value that does not fit its filter.</summary>
    [Fact]
    public void Normalize_WithAValueThatDoesNotFit_Throws()
    {
        var ex = Assert.Throws<DomainRuleException>(() => EmailCampaignSegment.Normalize(CampaignFilterType.Country, "ARM"));

        Assert.Equal(CampaignErrors.SegmentValueInvalid, ex.Code);
    }
}
