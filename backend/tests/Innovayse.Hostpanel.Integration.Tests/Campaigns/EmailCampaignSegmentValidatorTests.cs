using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Campaigns;

/// <summary>
/// Pins what <see cref="EmailCampaignSegmentValidator"/> decides on its own: a filter on the last sign-in is accepted where the
/// deployment owns its accounts and refused where a sign-on provider does, because the provider does not tell this product when
/// a person signed in. Runs without a database.
/// </summary>
public sealed class EmailCampaignSegmentValidatorTests
{
    /// <summary>Localizer that echoes keys, so no resource file is needed.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>The message key the sign-in refusal asks for.</summary>
    private const string SignInUnavailableKey = "text:CampaignSegmentSignInUnavailable";

    /// <summary>Builds the validator for a deployment that owns its accounts or does not.</summary>
    /// <param name="ownsItsAccounts">Whether the deployment signs people in itself.</param>
    /// <returns>The validator.</returns>
    private static EmailCampaignSegmentValidator ValidatorFor(bool ownsItsAccounts) =>
        new(Localizer, new ConfigurationAuthModeProvider(ownsItsAccounts));

    /// <summary>Where the deployment owns its accounts, a sign-in segment with a fitting number of days passes.</summary>
    [Fact]
    public void SignInSegment_InLocalMode_Passes()
    {
        var result = ValidatorFor(ownsItsAccounts: true).Validate(new CampaignSegmentDto(CampaignFilterType.NotSignedInForDays, "90"));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Where a sign-on provider owns the accounts, a sign-in segment is refused with its own sentence.</summary>
    [Fact]
    public void SignInSegment_InSsoMode_IsRefused()
    {
        var result = ValidatorFor(ownsItsAccounts: false).Validate(new CampaignSegmentDto(CampaignFilterType.NotSignedInForDays, "90"));

        result.Errors.Select(e => e.ErrorMessage).Should().Contain(SignInUnavailableKey);
    }

    /// <summary>The other filters do not depend on how the deployment signs people in.</summary>
    /// <param name="filterType">The filter of the segment.</param>
    /// <param name="filterValue">The value of the segment.</param>
    [Theory]
    [InlineData(CampaignFilterType.AllActive, null)]
    [InlineData(CampaignFilterType.ClientGroup, "4")]
    [InlineData(CampaignFilterType.Country, "AM")]
    public void OtherSegments_InSsoMode_Pass(CampaignFilterType filterType, string? filterValue)
    {
        var result = ValidatorFor(ownsItsAccounts: false).Validate(new CampaignSegmentDto(filterType, filterValue));

        result.IsValid.Should().BeTrue();
    }
}
