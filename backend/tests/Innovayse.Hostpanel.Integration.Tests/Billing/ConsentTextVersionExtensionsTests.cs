using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The wire values of <see cref="ConsentTextVersion"/>: each published member is accepted by its portal text, the texts
/// fit the consent's column, and anything else is never taken for a published version.
/// </summary>
public sealed class ConsentTextVersionExtensionsTests
{
    /// <summary>The portal's texts, written out literally; a member added to the enum without a text here fails the count below.</summary>
    private static readonly string[] PortalTexts = [ConsentTextVersionWire.AccountCard, ConsentTextVersionWire.CheckoutAgreement];

    /// <summary>Every enum member has exactly one portal text, the texts differ, fit the consent's column and are all published.</summary>
    [Fact]
    public void EveryMember_HasOneUniquePortalTextThatIsPublished()
    {
        Enum.GetValues<ConsentTextVersion>().Should().HaveCount(PortalTexts.Length, "a new member needs its portal text in this test and in the extension");
        PortalTexts.Should().OnlyHaveUniqueItems();
        PortalTexts.Should().OnlyContain(text => text.Length > 0 && text.Length <= CardChargeConsent.MaxTextVersionLength);
        PortalTexts.Should().OnlyContain(text => text.IsPublishedConsentTextVersion());
    }

    /// <summary>An unpublished, blank, missing or differently cased text is not published, and nothing throws.</summary>
    [Fact]
    public void UnpublishedTexts_AreNotPublished()
    {
        ((string?)null).IsPublishedConsentTextVersion().Should().BeFalse();
        string.Empty.IsPublishedConsentTextVersion().Should().BeFalse();
        "2026-11".IsPublishedConsentTextVersion().Should().BeFalse();
        ConsentTextVersionWire.AccountCard.ToUpperInvariant().IsPublishedConsentTextVersion().Should().BeFalse("the comparison is exact");
    }
}
