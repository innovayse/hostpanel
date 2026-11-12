using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The wire values of <see cref="ConsentTextVersion"/>: every member has one, they differ, they fit the consent's column,
/// and an undefined value or an unpublished text is never taken for a published version.
/// </summary>
public sealed class ConsentTextVersionExtensionsTests
{
    /// <summary>Every member of the enum has a non-empty wire value that fits the column of the consent, and no two share one.</summary>
    [Fact]
    public void EveryMember_HasAUniqueWireValueThatFitsTheConsent()
    {
        var wires = Enum.GetValues<ConsentTextVersion>().Select(version => version.ToWire()).ToList();

        wires.Should().OnlyContain(wire => !string.IsNullOrWhiteSpace(wire) && wire.Length <= CardChargeConsent.MaxTextVersionLength);
        wires.Should().OnlyHaveUniqueItems();
        wires.Should().OnlyContain(wire => wire.IsPublishedConsentTextVersion());
    }

    /// <summary>The wire values are the ones the portal sends, written out literally.</summary>
    [Fact]
    public void WireValues_AreThePortalsOwn()
    {
        ConsentTextVersion.AccountCard.ToWire().Should().Be(ConsentTextVersionWire.AccountCard);
        ConsentTextVersion.CheckoutAgreement.ToWire().Should().Be(ConsentTextVersionWire.CheckoutAgreement);
    }

    /// <summary>A value that is not a defined member has no wire value, and neither an unpublished nor a blank text is published; nothing throws.</summary>
    [Fact]
    public void UndefinedAndUnpublishedValues_AreNotPublished()
    {
        ((ConsentTextVersion)999).ToWire().Should().BeNull();
        ((string?)null).IsPublishedConsentTextVersion().Should().BeFalse();
        string.Empty.IsPublishedConsentTextVersion().Should().BeFalse();
        "2026-11".IsPublishedConsentTextVersion().Should().BeFalse();
        ConsentTextVersionWire.AccountCard.ToUpperInvariant().IsPublishedConsentTextVersion().Should().BeFalse("the comparison is exact");
    }
}
